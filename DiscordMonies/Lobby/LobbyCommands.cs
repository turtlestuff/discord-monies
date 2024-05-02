using System.ComponentModel;
using DiscordMonies.Board;
using DiscordMonies.Game;
using DiscordMonies.Remora;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

using System.Diagnostics;
using System.Drawing;
using DiscordMonies.Game.UnresolvedMatters.DiceRoll;

namespace DiscordMonies.Lobby;

[Group("lobby")]
public class LobbyCommands(LobbyService lobbyService, IFeedbackService feedbackService, IInteractionCommandContext ctx) : CommandGroup
{
    [Command("open")]
    [Description("Open a new lobby and start playing Discord Monies")]
    public async Task<IResult> OpenLobby()
    {
        var creatorId = ctx.Interaction.Member.Value.User.Value.ID;
        var guildId = ctx.Interaction.GuildID.Value;
        var lobbyId = lobbyService.OpenLobby(guildId, creatorId);

        var embed = new EmbedBuilder()
        {
            Title = "Ready to play Discord Monies?",
            Description =
                $"""
                {Mention.User(creatorId)} has opened **Lobby #{lobbyId}**!
                """,
            Footer = new($"This lobby will expire {Markdown.Timestamp(DateTime.UtcNow + TimeSpan.FromMinutes(5), TimestampStyle.RelativeTime)}"),
            Colour = Color.PaleGreen
        };

        var buttons = new ActionRowComponent([
            new ButtonComponent(ButtonComponentStyle.Primary, "Join",
                CustomID: CustomIDHelpers.CreateButtonIDWithState("JoinLobby", lobbyId.ToString())),
            new ButtonComponent(ButtonComponentStyle.Danger, "Leave",
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

public class LobbyInteractions(
    LobbyService lobbyService,
    GameService gameService,
    BoardService boardService,
    IFeedbackService feedbackService,
    IDiscordRestGuildAPI guildApi,
    IInteractionContext ctx,
    IDiscordRestChannelAPI channelApi) : InteractionGroup
{
    private static FeedbackMessageOptions Ephemeral = new(MessageFlags: MessageFlags.Ephemeral);

    [Button("JoinLobby")]
    public async Task<IResult> JoinLobby(string state)
    {
        var user = ctx.Interaction.Member.Value.User.Value;
        int lobbyId = int.Parse(state);

        var result = lobbyService.JoinLobby(lobbyId, user.ID);
        if (result.IsSuccess)
        {
            return await feedbackService.SendContextualAsync($"{Mention.User(user)} has joined lobby **#{lobbyId}**.");
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
    public async Task<IResult> DropLobby(string state)
    {
        var user = ctx.Interaction.Member.Value.User.Value;
        int lobbyId = int.Parse(state);

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
    public async Task<IResult> CloseLobby(string state)
    {
        var user = ctx.Interaction.Member.Value.User.Value;
        var channel = ctx.Interaction.Channel.Value;

        int lobbyId = int.Parse(state);

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
        var message = await feedbackService.SendContextualAsync($"Lobby **#{lobbyId}** has closed and the game is now starting.").AsThrowing();
        var thread = await channelApi.StartThreadFromMessageAsync(channel.ID.Value, message.ID, $"Game #{lobbyId}", AutoArchiveDuration.Hour).AsThrowing();

        var lobby = result.Entity;

        foreach (var player in lobby.Players)
        {
            await channelApi.AddThreadMemberAsync(thread.ID, player);
        }

        var gameState = await BuildNewGameState(lobby.Players, thread, lobby);
        gameService.OpenGame(gameState);
        return Result.Success;
    }

    private async Task<GameState> BuildNewGameState(List<Snowflake> playerIds, IChannel thread, ClosedLobby lobby)
    {
        var board = boardService.GetBoard("Standard");

        var gameState = new GameState
        {
            ThreadId = thread.ID,
            GuildId = lobby.GuildId,
            GameId = lobby.LobbyId,
            PlayerIds = playerIds,
            Board = new(board)
        };

        var shuffledColors = GamePlayer.ColorList.OrderBy(_ => Random.Shared.Next()).ToArray();

        foreach (var (playerId, color) in lobby.Players.Select((player, i) => (player, shuffledColors[i])))
        {
            var member = await guildApi.GetGuildMemberAsync(lobby.GuildId, playerId).AsThrowing();
            var user = member.User.Value;
            var displayName = member.Nickname.OrDefault() ?? user.Username;

            gameState.Players.Add(new()
            {
                GuildMember = member,
                User = user,
                Id = playerId,
                DisplayName = displayName,
                GameState = gameState,
                Money = board.Board.StartingMoney,
                Color = color
            });
        }

        gameState.CurrentlyResolvingMatter = new DiceRollMatter()
        {
            Player = gameState.Players[0]
        };

        return gameState;
    }
}
