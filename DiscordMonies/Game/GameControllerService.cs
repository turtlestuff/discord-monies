using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;

namespace DiscordMonies.Game;

public class GameControllerService(GameState state, IFeedbackService service)
{
    private async Task AdvanceTurn()
    {
        state.State = GameState.StateMachineState.Idle;
        if (state.Die.BothDiesHaveSameValue)
        {
            await service.SendAsync(state.ThreadId, $"{state.CurrentPlayer.DisplayName} rolled doubles so it's their turn to roll again!");
            return;
        }
        
        state.Die.Reset();
        state.CurrentPlayerIndex = (state.CurrentPlayerIndex + 1) % state.Players.Count;

        // TODO: Render map and send embed
        List<IMessageComponent> buttons = [
            new ButtonComponent(ButtonComponentStyle.Primary, "Roll",
                CustomID: CustomIDHelpers.CreateButtonID("roll")),
        ];

        if (false)
        {
            buttons.Insert(0, 
                new ButtonComponent(ButtonComponentStyle.Danger, "Bail",
                    CustomID: CustomIDHelpers.CreateButtonID("bail")));
        }
        
        await service.SendAsync(state.ThreadId, $"It is now the turn of {state.CurrentPlayer.DisplayName}", options: new()
        {
            MessageComponents = new([
                new ActionRowComponent(buttons)
            ])
        });
    }

    public async Task RollDie()
    {
        if (state.State != GameState.StateMachineState.Idle)
        {
            await service.SendContextualAsync("You can't roll now.");
            return;
        }

        // TODO: Jail logic
        var spaces = state.Die.Roll();
        await service.SendAsync(state.ThreadId,
            $"{state.CurrentPlayer.Username} rolled {state.Die.Die1} and {state.Die.Die2}");
        
        if (state.Die is { BothDiesHaveSameValue: true, SpeedingCount: 3 })
        {
            // Caught speeding!
            await SendToJail();
            await AdvanceTurn();
            return;
        }
        
        await MovePlayerRelative(state.CurrentPlayer, spaces);

        await ProcessPlayerLand();
    }

    public async Task Pay(GamePlayer? from, GamePlayer? to, int value)
    {
        
    }

    private async Task MovePlayerRelative(GamePlayer player, int spaces, bool passGoBonus = true)
    {
        var position = (player.State.Position + spaces) % state.Board.BoardInfo.BoardSpaces.Length;
        while (position < 0)
        {
            position = state.Board.BoardInfo.BoardSpaces.Length + position;
        }
        await MovePlayer(player, position, passGoBonus);
    }

    private async Task MovePlayer(GamePlayer player, int position, bool passGoBonus)
    {
        if (position < player.State.Position && passGoBonus)
        {
            await Pay(null, player, state.Board.PassGoValue);

            await service.SendAsync(state.ThreadId, $"{player.Username} was given {state.Board.PassGoValue.MoneyString()} for passing GO!");
        }

        player.State.Position = position;
    }

    private async Task ProcessPlayerLand()
    {
        await service.SendAsync(state.ThreadId,
            $"{state.CurrentPlayer.DisplayName} landed on {state.Board.BoardInfo.BoardSpaces[state.CurrentPlayer.State.Position].Name}");
        
        await AdvanceTurn();
    }

    private async Task SendToJail()
    {
        
    }
}