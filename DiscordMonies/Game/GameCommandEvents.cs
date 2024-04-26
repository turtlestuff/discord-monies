using DiscordMonies.Extensions;
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

        var playerType = context.Command.Command.Node
            .FindCustomAttributeOnLocalTree<RequiresPlayerAttribute>()?.PlayerType ?? PlayerType.None;

        if (playerType != PlayerType.None)
        {
            // The command must at least run inside a game thread
            if (!ctx.TryGetUserID(out var userId) || !ctx.TryGetChannelID(out var channelId) || service.GetGameState(channelId) is not GameState gameState)
            {
                return await Failed("You can't use this command outside of a game.", ct);
            }

            injector.GameState = gameState;

            if (playerType != PlayerType.PlayerOrSpectator)
            {
                // The command must at least be run by a player in the game
                if (!gameState.PlayerIds.Contains(userId))
                {
                    return await Failed("You can only use this command if you're a player in this game.", ct);
                }

                if (playerType == PlayerType.CurrentPlayer)
                {
                    if (gameState.CurrentPlayer.Id != userId)
                    {
                        return await Failed("You can only use this command if it's your turn.", ct);
                    }
                }
            }

            await service.AcquireGameLock(gameState);
        }

        await SendDeferredResponseIfNeeded(interactionCommandContext, ct);
        return Result.Success;
    }

    private async Task<Result> Failed(string message, CancellationToken ct)
    {
        await feedback.SendContextualAsync(message, options: MessageOptions.Ephemeral, ct: ct).AsThrowing();
        return new OperationCanceledException();
    }

    private static readonly InteractionMessageCallbackData Ephemeral = new(Flags: MessageFlags.Ephemeral);

    private async Task<Result> SendDeferredResponseIfNeeded(InteractionCommandContext commandContext, CancellationToken ct)
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
        // Use the same conditions as the pre-execution event so we don't end up with an unbalanced count on the semaphore
        if (ctx.TryGetUserID(out _) && ctx.TryGetChannelID(out var channelId) && service.GetGameState(channelId) is GameState gameState)
        {
            service.ReleaseGameLock(gameState);
        }

        return Task.FromResult(Result.Success);
    }
}