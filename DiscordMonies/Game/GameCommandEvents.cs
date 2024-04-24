using DiscordMonies.Extensions;
using DiscordMonies.Lobby;
using DiscordMonies.Remora;

using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace DiscordMonies.Game;

internal class GameCommandPreExecutionEvent(GameService service, GameStateInjector injector, IFeedbackService feedback, IInteractionContext ctx, IDiscordRestInteractionAPI interactionApi) : IPreExecutionEvent
{
    public async Task<Result> BeforeExecutionAsync(ICommandContext context, CancellationToken ct = default)
    {
        if (context is not InteractionCommandContext interactionCommandContext)
        {
            return Result.Success;
        }

        var commandType = context.Command.Command.Node.GroupType;
        if (commandType == typeof(LobbyCommands) || commandType == typeof(LobbyInteractions))
        {
            await SendDeferredResponseIfNeeded(interactionCommandContext, ct);
            return Result.Success;
        }

        if (ctx.TryGetChannelID(out var channelId) && service.GetGameState(channelId) is GameState gameState)
        {
            await SendDeferredResponseIfNeeded(interactionCommandContext, ct);
            injector.GameState = gameState;
            await service.AcquireGameLock(gameState);
            return Result.Success;
        }

        // Returing a failed result from this method prevents the command from running.
        // OperationCanceledException suppresses the warning log
        await feedback.SendContextualAsync("You can't use this command outside of a game.", options: MessageOptions.Ephemeral, ct: ct).AsThrowing();
        return new OperationCanceledException();
    }

    private static readonly InteractionMessageCallbackData Ephemeral = new(Flags: MessageFlags.Ephemeral);

    private async Task<IResult> SendDeferredResponseIfNeeded(InteractionCommandContext commandContext, CancellationToken ct)
    {
        var preparedCommand = commandContext.Command;
        var suppress = commandContext.Command.Command.Node
            .FindCustomAttributeOnLocalTree<SuppressInteractionResponseAttribute>()?.Suppress ?? false;

        if (!suppress && !commandContext.HasRespondedToInteraction)
        {
            var ephemeral = preparedCommand.Command.Node
                .FindCustomAttributeOnLocalTree<EphemeralAttribute>()?.IsEphemeral == true;

            var interactionResponse = await interactionApi.CreateInteractionResponseAsync(
                commandContext.Interaction.ID,
                commandContext.Interaction.Token,
                new InteractionResponse(
                    InteractionCallbackType.DeferredChannelMessageWithSource,
                    ephemeral ? new(Ephemeral) : default),
                ct: ct);

            if (!interactionResponse.IsSuccess)
            {
                return interactionResponse;
            }

            commandContext.HasRespondedToInteraction = true;
            commandContext.IsOriginalEphemeral = ephemeral;
        }

        return Result.Success;
    }
}

internal class GameCommandPostExecutionEvent(GameService service, IInteractionContext ctx) : IPostExecutionEvent
{
    public Task<Result> AfterExecutionAsync(ICommandContext context, IResult commandResult, CancellationToken ct = default)
    {
        if (ctx.TryGetChannelID(out var channelId) && service.GetGameState(channelId) is GameState state)
        {
            service.ReleaseGameLock(state);
        }

        return Task.FromResult(Result.Success);
    }
}