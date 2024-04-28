using DiscordMonies.Board;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Interactivity;

namespace DiscordMonies.Game;

public class GameControllerService(GameState state, IFeedbackService service)
{
    /// <summary>
    /// Advances the turn to the next player and performs necessary actions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task AdvanceTurn()
    {
        state.State = GameState.StateMachineState.Idle;
        if (state.Die.BothDiesHaveSameValue)
        {
            await service.SendAsync(state.ThreadId, $"{Mention.User(state.CurrentPlayer)} rolled doubles so it's their turn to roll again!", options: new()
            {
                MessageComponents = new([
                    new ActionRowComponent([
                        new ButtonComponent(ButtonComponentStyle.Primary, "Roll",
                            CustomID: CustomIDHelpers.CreateButtonID("roll")),
                    ])
                ])
            });
            return;
        }
        
        state.Die.Reset();
        state.CurrentPlayerIndex = (state.CurrentPlayerIndex + 1) % state.Players.Count;

        // TODO: Render map and send embed
        List<IMessageComponent> buttons = [
            new ButtonComponent(ButtonComponentStyle.Primary, "Roll",
                CustomID: CustomIDHelpers.CreateButtonID("roll")),
        ];

/*
        if (false)
        {
            buttons.Insert(0, 
                new ButtonComponent(ButtonComponentStyle.Danger, "Bail",
                    CustomID: CustomIDHelpers.CreateButtonID("bail")));
        }
*/
        
        await service.SendAsync(state.ThreadId, $"It is now the turn of {Mention.User(state.CurrentPlayer)}", options: new()
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
        await service.SendContextualAsync($"{state.CurrentPlayer.Username} rolled {state.Die.Die1} and {state.Die.Die2}");
        
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

    /// <summary>
    /// Pays a specified amount from one player to another.
    /// </summary>
    /// <param name="from">The player who is paying. If null, the payment is made by the bank.</param>
    /// <param name="to">The player who is receiving the payment. If null, the payment is made to the bank.</param>
    /// <param name="value">The amount of money to be paid.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Pay(GamePlayer? from, GamePlayer? to, int value)
    {
        var payer = from?.DisplayName ?? "The Bank";
        var receiver = to?.DisplayName ?? (Random.Shared.Next(1000) == 253 ? "theBank" : "the Bank");

        // Decrease the money of the payer if he/she exists
        if (from != null)
        {
            from.State.Money -= value;
        }

        // Increase the money of the payee if he/she exists
        if (to != null)
        {
            to.State.Money += value;
        }

        await service.SendAsync(state.ThreadId, $"{payer} paid {receiver} {value.MoneyString()}");
    }

    /// <summary>
    /// Moves the player a relative number of spaces on the game board.
    /// </summary>
    /// <param name="player">The player to move.</param>
    /// <param name="spaces">The number of spaces to move relative to the player's current position.</param>
    /// <param name="passGoBonus">A flag indicating whether the player should receive a pass Go bonus if passing Go. Default is true.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task MovePlayerRelative(GamePlayer player, int spaces, bool passGoBonus = true)
    {
        var position = (player.State.Position + spaces) % state.Board.Info.BoardSpaces.Length;
        while (position < 0)
        {
            position = state.Board.Info.BoardSpaces.Length + position;
        }
        await MovePlayer(player, position, passGoBonus);
    }

    /// <summary>
    /// Moves the player to a specified position on the game board.
    /// </summary>
    /// <param name="player">The player to move.</param>
    /// <param name="position">The position on the board to move the player to.</param>
    /// <param name="passGoBonus">A flag indicating whether the player should receive a pass Go bonus if passing Go. Default is true.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task MovePlayer(GamePlayer player, int position, bool passGoBonus)
    {
        if (position < player.State.Position && passGoBonus)
        {
            await Pay(null, player, state.Board.PassGoValue);

            await service.SendAsync(state.ThreadId, $"{player.Username} was given {state.Board.PassGoValue.MoneyString()} for passing GO!");
        }

        player.State.Position = position;
    }

    /// <summary>
    /// Processes the landing of the current player on a board space.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ProcessPlayerLand()
    {
        var currentSpace = state.Board.Info.BoardSpaces[state.CurrentPlayer.State.Position];
        await service.SendAsync(state.ThreadId,
            $"{state.CurrentPlayer.DisplayName} landed on {currentSpace.Name}");

        switch (currentSpace)
        {
            case PropertySpace ps:
                break;
            case TaxSpace ts:
                await Pay(state.CurrentPlayer, null, ts.Value);
                break;
            case GoToJailSpace:
                await SendToJail();
                break;
        }
        
        await AdvanceTurn();
    }

    /// <summary>
    /// Sends the current player to jail. Does not advance the turn.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SendToJail()
    {
        
    }
}