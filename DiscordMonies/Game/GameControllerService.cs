using DiscordMonies.Board;
using DiscordMonies.Extensions;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace DiscordMonies.Game;

public class GameControllerService(GameState state, IFeedbackService service)
{
    /// <summary>
    /// Advances the turn to the next player and performs necessary actions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task AdvanceTurn()
    {
        state.NextRequiredAction = GameAction.Roll;
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

    public async Task<Result> RollDie()
    {
        // TODO: Jail logic
        var spaces = state.Die.Roll();
        await service.SendContextualAsync($"{state.CurrentPlayer} rolled {state.Die.Die1} and {state.Die.Die2}");
        
        if (state.Die is { BothDiesHaveSameValue: true, SpeedingCount: 3 })
        {
            // Caught speeding!
            await SendToJail();
            await AdvanceTurn();
            return Result.Success;
        }
        
        await MovePlayerRelative(state.CurrentPlayer, spaces);

        await ProcessPlayerLand();
        return Result.Success;
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
            from.Money -= value;
        }

        // Increase the money of the payee if he/she exists
        if (to != null)
        {
            to.Money += value;
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
        var boardLength = state.Board.Spaces.Length;
        var position = (player.Position + spaces) % boardLength;
        while (position < 0)
        {
            position = boardLength + position;
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
        if (position < player.Position && passGoBonus)
        {
            await Pay(null, player, state.Board.PassGoValue);

            await service.SendAsync(state.ThreadId, $"{player} was given {state.Board.PassGoValue.MoneyString()} for passing GO!");
        }

        player.Position = position;
    }

    /// <summary>
    /// Processes the landing of the current player on a board space.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ProcessPlayerLand()
    {
        var currentSpace = state.CurrentPlayer.CurrentSpace;
        await service.SendAsync(state.ThreadId,
            $"{state.CurrentPlayer} landed on {currentSpace}");

        switch (currentSpace.Info)
        {
            case PropertySpace ps:
                if (!await ProcessPropertySpaceLand()) return;
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

    private async Task<bool> ProcessPropertySpaceLand()
    {
        var space = state.CurrentPlayer.CurrentSpace;
        var property = (PropertySpace) space.Info;
        if (space.Owner is null)
        {
            // Purchase this property?
            await service.SendAsync(state.ThreadId,
                $"{Mention.User(state.CurrentPlayer)} - **{space}** is available for purchase! Do you want to purchase it?",
                embeds: new([space.TitleDeedEmbed()]),
                options: new()
                {
                    MessageComponents = new([
                        new ActionRowComponent([
                            new ButtonComponent(ButtonComponentStyle.Success, $"Purchase {space} for {property.Value.MoneyString(false)}",
                                CustomID: CustomIDHelpers.CreateButtonID("purchase")),
                            new ButtonComponent(ButtonComponentStyle.Danger, "Send to Auction",
                                CustomID: CustomIDHelpers.CreateButtonID("auction"))
                        ])
                    ])
                });
            
            state.NextRequiredAction = GameAction.PurchaseDecision;
            
            return false;
        }

        if (space.Owner == state.CurrentPlayer)
        {
            
        }
        else if (space.IsMortgaged)
        {
            // Nothing happens because this space is mortgaged
        }
        else
        {
            // Pay rent!
            await Pay(state.CurrentPlayer, space.Owner, await CalculateRent(space));
        }

        return true;
    }

    public async Task<int> CalculateRent(Space ps)
    {
        return ps.Info switch
        {
            RoadSpace rs when await ColourSetOwner(rs) == ps.Owner && ps.BuiltHouses == 0 => ps.TitleDeed!.RentValues[0] * 2,
            RoadSpace => ps.TitleDeed!.RentValues[ps.BuiltHouses],
            
            TrainStationSpace => ps.TitleDeed!.RentValues[0] << (state.Board.Spaces
                .Where(space => space.Info is TrainStationSpace)
                .Select(space => space.Owner)
                .Count(owner => owner == ps.Owner) - 1),
            UtilitySpace => state.Board.Spaces
                .Where(space => space.Info is UtilitySpace)
                .Select(space => space.Owner)
                .Count(owner => owner == ps.Owner) == 1 ?
                state.Die * 4 : state.Die * 10,
            _ => throw new InvalidOperationException()
        };
    }

    public async Task<GamePlayer?> ColourSetOwner(RoadSpace ps)
    {
        var owners = state.Board.Spaces
            .Where(space => space.Info is RoadSpace rs && rs.Group == ps.Group)
            .Select(space => space.Owner)
            .Distinct().ToList();

        return owners.Count == 1 ? owners[0] : null;
    }

    /// <summary>
    /// Sends the current player to jail. Does not advance the turn.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SendToJail()
    {
        
    }
}