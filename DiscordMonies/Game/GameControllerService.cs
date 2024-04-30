using DiscordMonies.Board;
using DiscordMonies.Extensions;
using DiscordMonies.Remora;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
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
        if (state.Dice.DoubleRoll)
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
        
        state.Dice.Reset();
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
        var spaces = state.Dice.Roll();

        if (state.CurrentPlayer.JailTurns != -1 && !state.Dice.DoubleRoll)
        {
            // Jail logic
            state.CurrentPlayer.JailTurns++;
            if (state.CurrentPlayer.JailTurns == 3)
            {
                await service.SendContextualEmbedAsync(Embeds.FinalJailDieRolled(state.CurrentPlayer, state.Dice), new()
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
                await service.SendContextualEmbedAsync(Embeds.JailDieRolled(state.CurrentPlayer, state.Dice,
                    3 - state.CurrentPlayer.JailTurns));
                
                state.Dice.Reset();

                await AdvanceTurn();
            }
            return;
        }
        
        if (state.Dice is { DoubleRoll: true, SpeedingCount: 3 })
        {
            // Caught speeding!
            await service.SendContextualEmbedAsync(Embeds.SpeedingDieRolled(state.CurrentPlayer, state.Dice));
            await SendToJail();
            
            // Reset the die so that they don't get another Doubles roll
            state.Dice.Reset();
            
            await AdvanceTurn();
            return;
        }
        
        await MovePlayerRelative(state.CurrentPlayer, spaces);

        if (state.CurrentPlayer.JailTurns != -1)
        {
            // Rolled doubles to get out of jail!
            await service.SendContextualEmbedAsync(Embeds.JailDieDoublesRolled(state.CurrentPlayer, state.Dice,
                state.CurrentPlayer.CurrentSpace));
            state.CurrentPlayer.JailTurns = -1;
            
            // Reset the die so that they don't get another Doubles roll
            state.Dice.Reset();
        }
        else
        {
            await service.SendContextualEmbedAsync(Embeds.DieRolled(state.CurrentPlayer, state.Dice,
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
        if (value < 0) return await TryPay(to, from, -value);
        
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
        if (value < 0)
        {
            await Pay(to, from, -value);
            return;
        }
        
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
            
            UtilitySpace when bigRent => state.Dice * 10,
            UtilitySpace => CountOwned<UtilitySpace>(ps.Owner) switch
            {
                1 => state.Dice * 4,
                _ => state.Dice * 10
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
            await MovePlayerRelative(state.CurrentPlayer, state.Dice.Total);
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

    public async Task<IList<Space>> DevelopOptions(GamePlayer player, ColorSet colorSet)
    {
        var spaces = state.Board.Spaces.Where(x => x.Info is RoadSpace rs && rs.Group == colorSet.Index).ToList();
        if (await ColourSetOwner((RoadSpace) spaces.First().Info) != player)
        {
            return [];
        }

        return spaces;
    }

    public async Task DevelopProperties(GamePlayer player, ColorSet colorSet, IEnumerable<int> targets)
    {
        var spaces = state.Board.Spaces.Where(x => x.Info is RoadSpace rs && rs.Group == colorSet.Index).ToList();
        var usableTargets = targets.Take(spaces.Count).ToList();
        
        // Ensure no target is more than 1 away
        var min = usableTargets.Min();
        var max = usableTargets.Max();
        if (min < 0)
        {
            await service.SendContextualAsync(
                "Unable to develop properties as per your request. You can have a minimum of 0 houses on any property.", options: new()
                {
                    MessageFlags = new(MessageFlags.Ephemeral)
                });
            return;
        }

        if (max > 5)
        {
            await service.SendContextualAsync("Unable to develop properties as per your request. You can have a maximum of 1 hotel on any property.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return;
        }
        
        if (min + 2 <= max)
        {
            await service.SendContextualAsync("Unable to develop properties as per your request. You must build evenly on all properties in the set (all properties must have 2 houses before you can build 3 houses on one property)", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return;
        }
        
        if (await ColourSetOwner((RoadSpace) spaces.First().Info) != player)
        {
            await service.SendContextualAsync(Strings.UnableToDevelopOnColourSet(state.Board.GroupNames[colorSet.Index]));
            return;
        }

        var hotelsRemaining = state.Board.HotelsRemaining;
        var housesRemaining = state.Board.HousesRemaining;
        var price = 0;

        // Ignore mortgaged properties
        var spaceHouses = spaces.Select(x => x.IsMortgaged ? 0 : x.BuiltHouses).ToList();
        
        // Plan the build
        while (!spaceHouses.SequenceEqual(usableTargets))
        {
            // These two variables should always be within one of each other
            var spaceMax = spaceHouses.Max();
            var spaceMin = spaceHouses.Min();
            foreach (var (space, index) in spaceHouses.Select((space, index) => (space, index)).ToList())
            {
                var target = usableTargets[index];
                if (space == target) continue;
                
                if (space < target && (space != spaceMax || spaceMin == spaceMax))
                {
                    if (space == 4)
                    {
                        // About to build a hotel
                        if (hotelsRemaining == 0)
                        {
                            // Fail: Need to build a hotel but there are no more hotels
                            await service.SendContextualAsync("No more hotels are available to service your request. (No changes were made to your properties.)", options: new()
                            {
                                MessageFlags = new(MessageFlags.Ephemeral)
                            });
                            return;
                        }

                        spaceHouses[index] = space + 1;
                        housesRemaining += 4;
                        hotelsRemaining -= 1;

                        price += spaces[index].TitleDeed!.HotelCost;
                    }
                    else
                    {
                        if (housesRemaining == 0)
                        {
                            // Fail: Need to build a house but there are no more houses
                            await service.SendContextualAsync("No more houses are available to service your request. (No changes were made to your properties.)", options: new()
                            {
                                MessageFlags = new(MessageFlags.Ephemeral)
                            });
                            return;
                        }

                        spaceHouses[index] = space + 1;
                        housesRemaining -= 1;
                        price += spaces[index].TitleDeed!.HouseCost;
                    }
                }
                else if (space > target && space != spaceMin || spaceMin == spaceMax)
                {
                    if (space == 5)
                    {
                        // About to demolish a hotel
                        if (housesRemaining < 4)
                        {
                            // Fail: Need to demolish a hotel but there are no more houses
                            await service.SendContextualAsync("No more houses are available to service your request because demolishing a hotel requires 4 houses to be available. (No changes were made to your properties.)", options: new()
                            {
                                MessageFlags = new(MessageFlags.Ephemeral)
                            });
                            return;
                        }

                        spaceHouses[index] = space - 1;
                        housesRemaining -= 4;
                        hotelsRemaining += 1;

                        price -= spaces[index].TitleDeed!.HotelCost / 2;
                    }
                    else
                    {
                        // About to demolish a house
                        spaceHouses[index] = space - 1;
                        housesRemaining += 1;
                        price -= spaces[index].TitleDeed!.HouseCost / 2;
                    }
                }
            }
        }

        if (!await TryPay(player, null, price))
        {
            await service.SendContextualAsync(
                Strings.BalanceTooLow("Your balance is too low to complete the required development", player.Money,
                    price), options: new()
                {
                    MessageFlags = new(MessageFlags.Ephemeral)
                });
            return;
        }
        
        // Everything is okay!

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"Development on {state.Board.GroupNames[colorSet.Index]}")
            .WithDescription($"Development on {state.Board.GroupNames[colorSet.Index]} has been completed by **{player}**. The changes are as follows:");
        
        foreach (var (target, index) in usableTargets.Select((x, i) => (x, i)))
        {
            string FormatHouseNumber(int houseNumber)
            {
                return houseNumber switch
                {
                    0 => "0 houses",
                    1 => "1 house",
                    2 => "2 houses",
                    3 => "3 houses",
                    4 => "4 houses",
                    5 => "Hotel",
                    _ => throw new ArgumentOutOfRangeException(nameof(houseNumber), houseNumber, null)
                };
            }
            
            var space = spaces[index];
            if (space.IsMortgaged)
            {
                embedBuilder.AddField(space.ToString(), "Mortgaged", true);
                continue;
            }
            
            embedBuilder.AddField(space.ToString(), $"**Was:** {FormatHouseNumber(space.BuiltHouses)}\n**Now:** {FormatHouseNumber(target)}", true);
            space.BuiltHouses = target;
        }

        await service.SendContextualEmbedAsync(embedBuilder.Build().Get());
    }
}