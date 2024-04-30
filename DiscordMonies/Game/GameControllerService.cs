using DiscordMonies.Board;
using DiscordMonies.Extensions;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;

namespace DiscordMonies.Game;

public class GameControllerService(GameState state, IFeedbackService service, IInteractionContext context, BoardRenderer renderer)
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
            await service.SendAsync(state.ThreadId, $"{Mention.User(state.CurrentPlayer)}",
                embeds: new([Embeds.TurnAdvancedDoubles(state.CurrentPlayer)]),
                options: new()
            {
                Attachments = new([
                    new FileData("board.png", renderer.RenderToStream())
                ]),
                MessageComponents = new([
                    new ActionRowComponent([
                        Buttons.Roll()
                    ])
                ])
            });
            return;
        }
        
        state.Die.Reset();
        state.CurrentPlayerIndex = (state.CurrentPlayerIndex + 1) % state.Players.Count;

        // TODO: Render map and send embed
        List<IMessageComponent> buttons = [
            Buttons.Roll()
        ];
        
        if (state.CurrentPlayer.JailTurns != -1)
        {
            buttons.Insert(0, Buttons.Bail(state.Board.JailFine));
            
            // TODO: Get out of jail free card
        }
        
        await service.SendAsync(state.ThreadId, $"{Mention.User(state.CurrentPlayer)}",
            embeds: new([Embeds.TurnAdvanced(state.CurrentPlayer)]),
            options: new()
        {
            Attachments = new([
                new FileData("board.png", renderer.RenderToStream())
            ]),
            MessageComponents = new([
                new ActionRowComponent(buttons)
            ])
        });
    }

    public async Task RollDie()
    {
        var spaces = state.Die.Roll();

        if (state.CurrentPlayer.JailTurns != -1 && !state.Die.BothDiesHaveSameValue)
        {
            // Jail logic
            state.CurrentPlayer.JailTurns++;
            if (state.CurrentPlayer.JailTurns == 3)
            {
                await service.SendContextualEmbedAsync(Embeds.FinalJailDieRolled(state.CurrentPlayer, state.Die), new()
                {
                    MessageComponents = new([
                        new ActionRowComponent([
                            Buttons.Bail(state.Board.JailFine)                        
                        ])
                    ])
                });
                state.NextRequiredAction = GameAction.RequiredBailPayment;
            }
            else
            {
                await service.SendContextualEmbedAsync(Embeds.JailDieRolled(state.CurrentPlayer, state.Die,
                    3 - state.CurrentPlayer.JailTurns));
                
                state.Die.Reset();

                await AdvanceTurn();
            }
            return;
        }
        
        if (state.Die is { BothDiesHaveSameValue: true, SpeedingCount: 3 })
        {
            // Caught speeding!
            await service.SendContextualEmbedAsync(Embeds.SpeedingDieRolled(state.CurrentPlayer, state.Die));
            await SendToJail();
            
            // Reset the die so that they don't get another Doubles roll
            state.Die.Reset();
            
            await AdvanceTurn();
            return;
        }
        
        await MovePlayerRelative(state.CurrentPlayer, spaces);

        if (state.CurrentPlayer.JailTurns != -1)
        {
            // Rolled doubles to get out of jail!
            await service.SendContextualEmbedAsync(Embeds.JailDieDoublesRolled(state.CurrentPlayer, state.Die,
                state.CurrentPlayer.CurrentSpace));
            state.CurrentPlayer.JailTurns = -1;
            
            // Reset the die so that they don't get another Doubles roll
            state.Die.Reset();
        }
        else
        {
            await service.SendContextualEmbedAsync(Embeds.DieRolled(state.CurrentPlayer, state.Die,
                state.CurrentPlayer.CurrentSpace));
        }

        if (await ProcessPlayerLand())
        {
            await AdvanceTurn();
        }
    }

    public async Task BuyProperty()
    {
        var space = state.CurrentPlayer.CurrentSpace;
        var value = ((ValueSpace)space.Info).Value;
        if (!await TryPay(state.CurrentPlayer, null, value))
        {
            await service.SendContextualAsync(
                Strings.BalanceTooLow($"Your balance is too low to buy {space}", state.CurrentPlayer.Money, value));
            return;
        }
        
        if (!context.Interaction.Message.HasValue)
        {
            await service.SendContextualAsync($"Purchasing {space}...");
        }

        space.Owner = state.CurrentPlayer;
        await service.SendEmbedAsync(state.ThreadId, Embeds.PropertyObtained(state.CurrentPlayer, space));
        
        await AdvanceTurn();
    }

    /// <summary>
    /// Attempts to pay a specified amount from one player to another.
    /// </summary>
    /// <param name="from">The player who is paying. If null, the payment is made by the bank.</param>
    /// <param name="to">The player who is receiving the payment. If null, the payment is made to the bank.</param>
    /// <param name="value">The amount of money to be paid.</param>
    /// <returns>True if the player has enough funds to complete the transaction</returns>
    public async Task<bool> TryPay(GamePlayer? from, GamePlayer? to, int value)
    {
        var payer = from?.DisplayName ?? "The Bank";
        var receiver = to?.DisplayName ?? (Random.Shared.Next(1000) == 253 ? "theBank" : "the Bank");

        // Decrease the money of the payer if they exist
        if (from != null)
        {
            if (from.Money < value)
            {
                return false;
            }

            from.Money -= value;
        }

        // Increase the money of the payee if they exist
        if (to != null)
        {
            to.Money += value;
        }

        await service.SendAsync(state.ThreadId, $"{payer} paid {receiver} {value.MoneyString()}");
        return true;
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

        // Decrease the money of the payer if they exist
        if (from != null)
        {
            from.Money -= value;
        }

        // Increase the money of the payee if they exist
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
        await MovePlayer(player, position, passGoBonus && spaces >= 0);
    }

    /// <summary>
    /// Moves the player to a specified position on the game board.
    /// </summary>
    /// <param name="player">The player to move.</param>
    /// <param name="position">The position on the board to move the player to.</param>
    /// <param name="passGoBonus">A flag indicating whether the player should receive a pass Go bonus if passing Go. Default is true.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task MovePlayer(GamePlayer player, int position, bool passGoBonus = true)
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
    /// <returns>A task representing the asynchronous operation. Returns true if the turn should be advanced, false otherwise.</returns>
    private async Task<bool> ProcessPlayerLand(bool bigRent = false)
    {
        var currentSpace = state.CurrentPlayer.CurrentSpace;

        switch (currentSpace.Info)
        {
            case DrawCardSpace:
                return await ProcessDrawCardSpaceLand();
            case PropertySpace:
                return await ProcessPropertySpaceLand(bigRent);
            case TaxSpace ts:
                await Pay(state.CurrentPlayer, null, ts.Value);
                break;
            case GoToJailSpace:
                await SendToJail();
                break;
        }

        return true;
    }

    private async Task<bool> ProcessDrawCardSpaceLand()
    {
        var space = state.CurrentPlayer.CurrentSpace;
        
        return await DrawCard(space.Info switch
        {
            DrawChanceSpace => state.Board.GambleCards,
            DrawChestSpace => state.Board.TreasureCards,
            _ => throw new ArgumentOutOfRangeException()
        });
    }

    /// <summary>
    /// Draws a card from the specified card stack and performs the corresponding action.
    /// </summary>
    /// <param name="stack">The card stack from which to draw the card.</param>
    /// <returns>A task representing the asynchronous operation. Returns true if the turn should be advanced, false otherwise.</returns>
    private async Task<bool> DrawCard(CardStack stack)
    {
        var card = stack.DrawCard();
        await service.SendEmbedAsync(state.ThreadId,
            Embeds.CardDrawn(state.CurrentPlayer, stack == state.Board.GambleCards, state.Board, card));

        // Complete the action
        var parts = card.Command.Split(' ');
        switch (parts[0])
        {
            case "arrest":
                await SendToJail();
                return true;

            case "give":
                await Pay(null, state.CurrentPlayer, int.Parse(parts[1]));
                return true;

            case "pay":
                await Pay(state.CurrentPlayer, null, int.Parse(parts[1]));
                return true;

            case "repairs":
                {
                    var houseValue = int.Parse(parts[1]);
                    var hotelValue = int.Parse(parts[2]);

                    var total = state.Board.Spaces.Aggregate(0, (currentTotal, space) =>
                    {
                        if (space.Info is not RoadSpace)
                        {
                            return currentTotal;
                        }
                        
                        if (space.Owner != state.CurrentPlayer)
                        {
                            return currentTotal;
                        }

                        if (space.BuiltHouses == 5)
                        {
                            return currentTotal + hotelValue;
                        }

                        return currentTotal + space.BuiltHouses * houseValue;
                    });

                    await Pay(state.CurrentPlayer, null, total);
                    return true;
                }

            case "warp":
                await MovePlayer(state.CurrentPlayer, int.Parse(parts[1]));
                await service.SendEmbedAsync(state.ThreadId,
                    Embeds.GenericLanding(state.CurrentPlayer, state.CurrentPlayer.CurrentSpace));
                return await ProcessPlayerLand();

            case "warprel":
                await MovePlayerRelative(state.CurrentPlayer, int.Parse(parts[1]));
                await service.SendEmbedAsync(state.ThreadId,
                    Embeds.GenericLanding(state.CurrentPlayer, state.CurrentPlayer.CurrentSpace));
                return await ProcessPlayerLand();

            case "jailfree":
                // TODO: Implement Get Out of Jail Free cards
                return true;

            case "paychance":
                // TODO: Implement Take Card or Pay
                // waiting = Waiting.ForTakeCardOrPayDecision;
                // payOrTakeCardVal = int.Parse(parts[1]);
                // payOrTakeCardType = CardType.Chance;
                return true;

            case "advance":
                var loc = parts[1] switch
                {
                    "utility" => state.Board.Spaces.FirstIndexAfterIndex(state.CurrentPlayer.Position,
                        s => s.Info is UtilitySpace),
                    "station" => state.Board.Spaces.FirstIndexAfterIndex(state.CurrentPlayer.Position,
                        s => s.Info is TrainStationSpace),
                    _ => throw new ArgumentException("Invalid type for `advance`")
                };

                await MovePlayer(state.CurrentPlayer, loc);
                await service.SendEmbedAsync(state.ThreadId,
                    Embeds.GenericLanding(state.CurrentPlayer, state.CurrentPlayer.CurrentSpace));
                return await ProcessPlayerLand(true);

            case "recvall":
                var paymentAmount = int.Parse(parts[1]);
                foreach (var player in state.Players.Where(x => x != state.CurrentPlayer))
                {
                    await Pay(player, state.CurrentPlayer, paymentAmount);
                }
                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// Processes the landing of a player on a property space and performs the necessary actions.
    /// </summary>
    /// <param name="bigRent">A boolean indicating if rent is doubled for train stations, or if the rent is 10 times the amount shown on the die for utilities.</param>
    /// <returns>A task representing the asynchronous operation. Returns true if the turn should be advanced, false otherwise.</returns>
    private async Task<bool> ProcessPropertySpaceLand(bool bigRent)
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
                            Buttons.Purchase(space.ToString(), property.Value),
                            Buttons.SendToAuction()
                        ])
                    ])
                });
            
            state.NextRequiredAction = GameAction.PurchaseDecision;
            
            return false;
        }

        if (space.Owner == state.CurrentPlayer)
        {
            // Nothing happens because this player owns this space
        }
        else if (space.IsMortgaged)
        {
            // Nothing happens because this space is mortgaged
        }
        else
        {
            // Pay rent!
            await Pay(state.CurrentPlayer, space.Owner, await CalculateRent(space, bigRent));
        }

        return true;
    }

    /// <summary>
    /// Calculates the rent for a given space.
    /// </summary>
    /// <param name="ps">The space for which to calculate the rent.</param>
    /// <param name="bigRent">A boolean indicating if rent is doubled for train stations, or if the rent is 10 times the amount shown on the die for utilities.</param>
    /// <returns>The calculated rent amount.</returns>
    public async Task<int> CalculateRent(Space ps, bool bigRent)
    {
        return ps.Info switch
        {
            RoadSpace rs when await ColourSetOwner(rs) == ps.Owner && ps.BuiltHouses == 0 => ps.TitleDeed!.RentValues[0] * 2,
            RoadSpace => ps.TitleDeed!.RentValues[ps.BuiltHouses],
            
            TrainStationSpace when bigRent => await CalculateRent(ps, false) * 2,
            TrainStationSpace => ps.TitleDeed!.RentValues[0] << (CountOwned<TrainStationSpace>(ps.Owner) - 1),
            
            UtilitySpace when bigRent => state.Die * 10,
            UtilitySpace => CountOwned<UtilitySpace>(ps.Owner) switch
            {
                1 => state.Die * 4,
                _ => state.Die * 10
            },
            _ => throw new InvalidOperationException()
        };
        
        int CountOwned<T>(GamePlayer? player) where T : BoardSpace =>
            state.Board.Spaces
                .Where(space => space.Info is T)
                .Select(space => space.Owner)
                .Count(owner => owner == player);
    }

    /// <summary>
    /// Determines the owner of a colour set.
    /// </summary>
    /// <param name="ps">The road space whose colour group is being checked.</param>
    /// <returns>The owner of the colour set, or null if there is no owner.</returns>
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
        await MovePlayer(state.CurrentPlayer, state.Board.Spaces.Select((space, i) => (space, i))
            .First(x => x.space.Info is JailSpace).i, false);

        state.CurrentPlayer.JailTurns = 0;

        await service.SendEmbedAsync(state.ThreadId, Embeds.SentToJail(state.CurrentPlayer));
    }

    public async Task SendPropertyToAuction()
    {
        var space = state.CurrentPlayer.CurrentSpace;
        await service.SendContextualAsync($"Sending {space} to auction...");

        await service.SendAsync(state.ThreadId,
            $"Auction is currently not supported. Advancing the turn to the next player...");
        
        await AdvanceTurn();
    }

    public async Task Bail()
    {
        if (state.CurrentPlayer.JailTurns == -1)
        {
            await service.SendContextualAsync("You can't bail from jail right now");
            return;
        }
        
        if (state.NextRequiredAction == GameAction.RequiredBailPayment)
        {
            if (!context.Interaction.Message.HasValue)
            {
                await service.SendContextualAsync("Bailing from jail...");
            }

            state.CurrentPlayer.JailTurns = -1;
            await Pay(state.CurrentPlayer, null, state.Board.JailFine);
            await MovePlayerRelative(state.CurrentPlayer, state.Die.Total);
            await service.SendEmbedAsync(state.ThreadId, Embeds.JailBail(state.CurrentPlayer, state.Board.JailFine, state.CurrentPlayer.CurrentSpace));
            if (await ProcessPlayerLand())
            {
                await AdvanceTurn();
            }
        }
        else
        {
            if (!await TryPay(state.CurrentPlayer, null, state.Board.JailFine))
            {
                await service.SendContextualAsync(Strings.BalanceTooLow("Your balance is too low to bail from jail", state.CurrentPlayer.Money, state.Board.JailFine));
                return;
            }

            if (!context.Interaction.Message.HasValue)
            {
                await service.SendContextualAsync("Bailing from jail...");
            }
            
            state.CurrentPlayer.JailTurns = -1;

            await service.SendEmbedAsync(state.ThreadId, Embeds.JailBail(state.CurrentPlayer, state.Board.JailFine),
                new()
                {
                    MessageComponents = new([
                        new ActionRowComponent([
                            Buttons.Roll()
                        ])
                    ])
                });
        }
    }
}