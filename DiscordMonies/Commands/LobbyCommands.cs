using DiscordMonies.Extensions;
using DiscordMonies.Services;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Interactivity;
using Remora.Results;

using System.Diagnostics;

namespace DiscordMonies.Commands;

[Group("lobby")]
internal class LobbyCommands(LobbyService lobbyService, IFeedbackService feedbackService, IInteractionCommandContext ctx) : CommandGroup
{
    [Command("open")]
    public async Task<IResult> OpenLobby()
    {
        var lobbyId = lobbyService.OpenLobby(ctx.Interaction.User.Value.ID);

        var embed = new EmbedBuilder()
        {

        };

        var buttons = new ActionRowComponent([
            new ButtonComponent(ButtonComponentStyle.Primary, "Join",
                CustomID: CustomIDHelpers.CreateButtonIDWithState("JoinLobby", lobbyId.ToString())),
            new ButtonComponent(ButtonComponentStyle.Danger, "Drop",
                CustomID: CustomIDHelpers.CreateButtonIDWithState("DropLobby", lobbyId.ToString())),
            new ButtonComponent(ButtonComponentStyle.Success, "Close and start",
                CustomID: CustomIDHelpers.CreateButtonIDWithState("CloseLobby", lobbyId.ToString()))
            ]);

        return await feedbackService.SendContextualEmbedAsync(embed.Build().Get(), new()
        {
            MessageComponents = new([buttons])
        });
    }
}

internal class LobbyInteractions(LobbyService lobbyService, IFeedbackService feedbackService, IInteractionContext ctx, IDiscordRestChannelAPI channelApi) : InteractionGroup
{
    private static FeedbackMessageOptions Ephemeral = new(MessageFlags: MessageFlags.Ephemeral);

    [Button("JoinLobby")]
    [SuppressInteractionResponse(true)]
    public async Task<IResult> JoinLobby(int lobbyId)
    {
        var user = ctx.Interaction.User.Value;
        var result = lobbyService.JoinLobby(lobbyId, user.ID);
        if (result.IsSuccess)
        {
            return await feedbackService.SendContextualAsync($"{Mention.User(user)} has joined game **#{lobbyId}**.");
        }
        else
        {
            return await feedbackService.SendContextualAsync(result.Error switch
            {
                UserCannotJoinDropClose => "You've already joined this lobby.",
                LobbyIsClosed => "This lobby is already closed.",
                _ => throw new UnreachableException()
            }, options: Ephemeral);
        }
    }

    [Button("DropLobby")]
    [SuppressInteractionResponse(true)]
    public async Task<IResult> DropLobby(int lobbyId)
    {
        var user = ctx.Interaction.User.Value;
        var result = lobbyService.DropLobby(lobbyId, user.ID);
        if (result.IsSuccess)
        {
            return await feedbackService.SendContextualAsync($"{Mention.User(user)} has left game **#{lobbyId}**.");
        }
        else
        {
            return await feedbackService.SendContextualAsync(result.Error switch
            {
                UserCannotJoinDropClose => "You're not in this this lobby.",
                LobbyIsClosed => "This lobby is already closed.",
                _ => throw new UnreachableException()
            }, options: Ephemeral);
        }
    }

    [Button("CloseLobby")]
    [SuppressInteractionResponse(true)]
    public async Task<IResult> CloseLobby(int lobbyId)
    {
        var user = ctx.Interaction.User.Value;
        var channel = ctx.Interaction.Channel.Value;
        var message = ctx.Interaction.Message.Value; 

        var result = lobbyService.CloseLobby(lobbyId, user.ID);
        if (!result.IsSuccess)
        {
            return await feedbackService.SendContextualAsync(result.Error switch
            {
                UserCannotJoinDropClose => "You're not in this this lobby.",
                LobbyIsClosed => "This lobby is already closed.",
                _ => throw new UnreachableException()
            }, options: Ephemeral);
        }

        await feedbackService.SendContextualAsync($"Game **#{lobbyId}** has closed and the game is now starting.").AsThrowing();
        var thread = await channelApi.StartThreadFromMessageAsync(channel.ID.Value, message.ID, $"Discord Monies Game #{lobbyId}", default).AsThrowing();
        
        return Result.Success;
    }
}
