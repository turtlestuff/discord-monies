using DiscordMonies.Board;
using DiscordMonies.Extensions;
using DiscordMonies.Game.Trade;
using DiscordMonies.Game.UnresolvedMatters;
using DiscordMonies.Game.UnresolvedMatters.AuctionMatter;
using DiscordMonies.Game.UnresolvedMatters.DiceRoll;
using DiscordMonies.Game.UnresolvedMatters.MortgageTransfer;
using DiscordMonies.Game.UnresolvedMatters.Payment;
using DiscordMonies.Game.UnresolvedMatters.PayOrGamble;
using DiscordMonies.Game.UnresolvedMatters.PurchaseDecision;
using DiscordMonies.Game.UnresolvedMatters.RequiredBailPayment;
using DiscordMonies.Game.UnresolvedMatters.RequiredBailPaymentComplete;
using DiscordMonies.Remora;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Results;

namespace DiscordMonies.Game;

public class GameControllerService(GameState state, IFeedbackService service, IInteractionContext context, BoardRenderer renderer, GameService gameService, IServiceProvider serviceProvider)
{
    /// <summary>
    /// Advances the turn to the next player and performs necessary actions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProcessNextUnresolvedMatter()
    {
        if (state.UnresolvedMatters.Count == 0)
        {
            if (!state.Dice.DoubleRoll)
            {
                state.CurrentPlayerIndex = (state.CurrentPlayerIndex + 1) % state.Players.Count;
                state.Dice.Reset();
            }
            state.CurrentlyResolvingMatter = new DiceRollMatter()
            {
                Player = state.CurrentPlayer
            };
        }
        else
        {
            state.CurrentlyResolvingMatter = state.UnresolvedMatters[0];
            state.UnresolvedMatters.RemoveAt(0);
        }
        
        await ProcessCurrentlyUnresolvedMatter();
    }

    private async Task ProcessCurrentlyUnresolvedMatter()
    {
        var specificType = typeof(IUnresolvedMatterResolver<>).MakeGenericType(state.CurrentlyResolvingMatter.GetType());
        var resolver = serviceProvider.GetRequiredService(specificType);

        var type = resolver.GetType();
        var resolveMethod = type.GetMethod("Resolve")!;
        await (Task) resolveMethod.Invoke(resolver, [state.CurrentlyResolvingMatter])!;
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
                state.UnresolvedMatters.Add(new RequiredBailPaymentMatter()
                {
                    Player = state.CurrentPlayer
                });
            }
            else
            {
                await service.SendContextualEmbedAsync(Embeds.JailDieRolled(state.CurrentPlayer, state.Dice,
                    3 - state.CurrentPlayer.JailTurns));
                
                state.Dice.Reset();
            }
            
            await ProcessNextUnresolvedMatter();
            return;
        }
        
        if (state.Dice is { DoubleRoll: true, SpeedingCount: 3 })
        {
            // Caught speeding!
            await service.SendContextualEmbedAsync(Embeds.SpeedingDieRolled(state.CurrentPlayer, state.Dice));
            await SendToJail();
            
            // Reset the die so that they don't get another Doubles roll
            state.Dice.Reset();
            
            await ProcessNextUnresolvedMatter();
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

        await ProcessPlayerLand();
        await ProcessNextUnresolvedMatter();
    }

    public async Task BuyProperty()
    {
        if (state.CurrentlyResolvingMatter is not PurchaseDecisionMatter matter)
        {
            return;
        }
        
        var space = matter.Space;
        var value = ((ValueSpace)space.Info).Value;
        if (!await TryPay(matter.Purchaser, null, value))
        {
            await service.SendContextualAsync(
                Strings.BalanceTooLow($"You do not have enough funds to buy {space}", matter.Purchaser.Money, value));
            return;
        }
        
        if (!context.Interaction.Message.HasValue)
        {
            await service.SendContextualAsync($"Purchasing {space}...");
        }

        space.Owner = matter.Purchaser;
        await service.SendEmbedAsync(state.ThreadId, Embeds.PropertyObtained(matter.Purchaser, space));
        
        await ProcessNextUnresolvedMatter();
    }

    /// <summary>
    /// Attempts to pay a specified amount from one player to another.
    /// </summary>
    /// <param name="from">The player who is paying. If null, the payment is made by the bank.</param>
    /// <param name="to">The player who is receiving the payment. If null, the payment is made to the bank.</param>
    /// <param name="value">The amount of money to be paid.</param>
    /// <returns>True if the player has enough funds to complete the transaction</returns>
    public async Task<bool> TryPay(GamePlayer? from, GamePlayer? to, int value, bool bankruptcyPayment = false)
    {
        if (value < 0) return await TryPay(to, from, -value, bankruptcyPayment);
        
        var payer = from?.DisplayName ?? "The Bank";
        var receiver = to?.DisplayName ?? (Random.Shared.Next(1000) == 253 ? "theBank" : "The Bank");

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

        await service.SendAsync(state.ThreadId, $"{Emoji.FlyingMoney} {value.MoneyString()}: **{payer}** {Emoji.ArrowRight} **{receiver}**");

        if (!bankruptcyPayment && state.CurrentlyResolvingMatter is PaymentMatter pm && pm.From == to && to?.Money >= 0)
        {
            await ProcessCurrentlyUnresolvedMatter();
        }
        
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
        
        state.UnresolvedMatters.Insert(0, new PaymentMatter()
        {
            From = from,
            To = to,
            Amount = value
        });
    }

    /// <summary>
    /// Moves the player a relative number of spaces on the game board.
    /// </summary>
    /// <param name="player">The player to move.</param>
    /// <param name="spaces">The number of spaces to move relative to the player's current position.</param>
    /// <param name="passGoBonus">A flag indicating whether the player should receive a pass Go bonus if passing Go. Default is true.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task MovePlayerRelative(GamePlayer player, int spaces, bool passGoBonus = true)
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
            await TryPay(null, player, state.Board.PassGoValue);

            await service.SendAsync(state.ThreadId, $"{player} was given {state.Board.PassGoValue.MoneyString()} for passing GO!");
        }

        player.Position = position;
    }

    /// <summary>
    /// Processes the landing of the current player on a board space.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProcessPlayerLand(bool bigRent = false)
    {
        var currentSpace = state.CurrentPlayer.CurrentSpace;

        switch (currentSpace.Info)
        {
            case DrawCardSpace:
                await ProcessDrawCardSpaceLand();
                return;
            case PropertySpace:
                await ProcessPropertySpaceLand(bigRent);
                return;
            case TaxSpace ts:
                await Pay(state.CurrentPlayer, null, ts.Value);
                return;
            case GoToJailSpace:
                await SendToJail();
                return;
        }
    }

    private async Task ProcessDrawCardSpaceLand()
    {
        var space = state.CurrentPlayer.CurrentSpace;
        
        await DrawCard(space.Info switch
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
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task DrawCard(CardStack stack)
    {
        LuckCard card;
        do
        {
            card = stack.DrawCard();
        } while (stack.JailCardOwner is not null && card.Command.StartsWith("jailcard"));
        
        await service.SendEmbedAsync(state.ThreadId,
            Embeds.CardDrawn(state.CurrentPlayer, stack == state.Board.GambleCards, state.Board, card));

        // Complete the action
        var parts = card.Command.Split(' ');
        switch (parts[0])
        {
            case "arrest":
                await SendToJail();
                return;

            case "give":
                await Pay(null, state.CurrentPlayer, int.Parse(parts[1]));
                return;

            case "pay":
                await Pay(state.CurrentPlayer, null, int.Parse(parts[1]));
                return;

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

                        if (space.BuiltHouses < 0)
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
                    return;
                }

            case "warp":
                await MovePlayer(state.CurrentPlayer, int.Parse(parts[1]));
                await service.SendEmbedAsync(state.ThreadId,
                    Embeds.GenericLanding(state.CurrentPlayer, state.CurrentPlayer.CurrentSpace));
                await ProcessPlayerLand();
                return;

            case "warprel":
                await MovePlayerRelative(state.CurrentPlayer, int.Parse(parts[1]));
                await service.SendEmbedAsync(state.ThreadId,
                    Embeds.GenericLanding(state.CurrentPlayer, state.CurrentPlayer.CurrentSpace));
                await ProcessPlayerLand();
                return;

            case "jailfree":
                stack.JailCardOwner = state.CurrentPlayer;
                return;

            case "paychance":
                state.UnresolvedMatters.Add(new PayOrGambleMatter()
                {
                    Player = state.CurrentPlayer,
                    PayValue = int.Parse(parts[1]),
                    CardStack = state.Board.GambleCards
                });
                return;

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
                await ProcessPlayerLand(true);
                return;

            case "recvall":
                var paymentAmount = int.Parse(parts[1]);
                foreach (var player in state.Players.Where(x => x != state.CurrentPlayer))
                {
                    await Pay(player, state.CurrentPlayer, paymentAmount);
                }

                return;
            default:
                return;
        }
    }

    /// <summary>
    /// Processes the landing of a player on a property space and performs the necessary actions.
    /// </summary>
    /// <param name="bigRent">A boolean indicating if rent is doubled for train stations, or if the rent is 10 times the amount shown on the die for utilities.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task ProcessPropertySpaceLand(bool bigRent)
    {
        var space = state.CurrentPlayer.CurrentSpace;
        if (space.Owner is null)
        {
            state.UnresolvedMatters.Add(new PurchaseDecisionMatter()
            {
                Purchaser = state.CurrentPlayer,
                Space = space
            });
            return;
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
            RoadSpace rs when ColorSetOwner(rs) == ps.Owner && ps.BuiltHouses == 0 => ps.TitleDeed!.RentValues[0] * 2,
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
    public GamePlayer? ColorSetOwner(PropertySpace space) 
        => (space switch
        {
            RoadSpace ps =>
                state.Board.Spaces
                .Where(space => space.Info is RoadSpace rs && rs.Group == ps.Group),

            TrainStationSpace =>
                state.Board.Spaces
                .Where(space => space.Info is TrainStationSpace),

            UtilitySpace =>
                state.Board.Spaces
                .Where(space => space.Info is UtilitySpace),

            _ => throw new ArgumentException("Unexpected space type.", nameof(space))
        })
        .Select(space => space.Owner)
        .Distinct()
        .OnlyOrDefault();

    public GamePlayer? ColorSetOwner(ColorSet colorSet) =>
        state.Board.Spaces
        .Where(s => s.Info is RoadSpace rs && rs.Group == colorSet.Index)
        .Select(s => s.Owner)
        .Distinct()
        .OnlyOrDefault();

    /// <summary>
    /// Sends the current player to jail. Does not advance the turn.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SendToJail()
    {
        await MovePlayer(state.CurrentPlayer, state.Board.Spaces.Select((space, i) => (space, i))
            .First(x => x.space.Info is JailSpace).i, false);

        state.CurrentPlayer.JailTurns = 0;
        
        // Reset the dice so that they don't get another turn
        state.Dice.Reset();

        await service.SendEmbedAsync(state.ThreadId, Embeds.SentToJail(state.CurrentPlayer));
    }

    public async Task SendPropertyToAuction()
    {
        var space = state.CurrentPlayer.CurrentSpace;
        await service.SendContextualAsync($"Sending {space} to auction...");

        state.UnresolvedMatters.Add(new AuctionMatter
        {
            AuctionSpace = space,
            Player = state.CurrentPlayer,
            BiddingPlayers = state.Players
        });
        await ProcessNextUnresolvedMatter();
    }

    public async Task Bail()
    {
        if (state.CurrentPlayer.JailTurns == -1)
        {
            await service.SendContextualAsync("You can't bail from jail right now.");
            return;
        }
        
        if (state.CurrentlyResolvingMatter is RequiredBailPaymentMatter)
        {
            if (!context.Interaction.Message.HasValue)
            {
                await service.SendContextualAsync("Bailing from jail...");
            }

            state.CurrentPlayer.JailTurns = -1;
            await Pay(state.CurrentPlayer, null, state.Board.JailFine);
            state.UnresolvedMatters.Add(new RequiredBailPaymentCompleteMatter()
            {
                Player = state.CurrentPlayer,
                Spaces = state.Dice.Total,
                JailCard = false
            });
            await ProcessNextUnresolvedMatter();
        }
        else
        {
            if (!await TryPay(state.CurrentPlayer, null, state.Board.JailFine))
            {
                await service.SendContextualAsync(Strings.BalanceTooLow("You do not have enough funds to bail from jail", state.CurrentPlayer.Money, state.Board.JailFine));
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

    public async Task UseJailCard(string type)
    {
        var cardStack = type switch
        {
            "gamble" => state.Board.GambleCards,
            "treasure" => state.Board.TreasureCards,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        if (cardStack.JailCardOwner != state.CurrentPlayer)
        {
            await service.SendContextualAsync("You don't own the Get Out of Jail Free card.");
            return;
        }
        
        if (state.CurrentPlayer.JailTurns == -1)
        {
            await service.SendContextualAsync("You can't use your Get Out of Jail Free card right now.");
            return;
        }
        
        if (!context.Interaction.Message.HasValue)
        {
            await service.SendContextualAsync("Using Get Out of Jail Free card...");
        }
        
        state.CurrentPlayer.JailTurns = -1;
        cardStack.JailCardOwner = null;
        
        if (state.CurrentlyResolvingMatter is RequiredBailPaymentMatter)
        {
            // Immediately move the amount shown on the die
            state.UnresolvedMatters.Add(new RequiredBailPaymentCompleteMatter()
            {
                Player = state.CurrentPlayer,
                Spaces = state.Dice.Total,
                JailCard = true
            });
            await ProcessNextUnresolvedMatter();
        }
        else
        {
            // Invite the player to roll
            await service.SendEmbedAsync(state.ThreadId, Embeds.JailBailCard(state.CurrentPlayer),
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

    public enum NotDevelopableReason
    {
        Developable,
        NotOwned,
        Mortgaged
    }

    public IList<Space> DevelopOptions(GamePlayer player, ColorSet colorSet, out NotDevelopableReason reason)
    {
        var spaces = SpacesInColorSet(colorSet).ToList();
        if (ColorSetOwner((RoadSpace) spaces.First().Info) != player)
        {
            reason = NotDevelopableReason.NotOwned;
            return [];
        }

        if (spaces.Any(x => x.IsMortgaged))
        {
            reason = NotDevelopableReason.Mortgaged;
            return [];
        }

        reason = NotDevelopableReason.Developable;
        return spaces;
    }

    public async Task DevelopProperties(GamePlayer player, ColorSet colorSet, IEnumerable<int> targets)
    {
        if (ColorSetOwner(colorSet) != player)
        {
            await service.SendContextualAsync(Strings.UnableToDevelopOnColourSet(state.Board.GroupNames[colorSet.Index]));
            return;
        }

        var spaces = SpacesInColorSet(colorSet).ToList();
        var priceMaybe = EnsureDevelopmentIsPossible(spaces, targets);

        if (!priceMaybe.IsSuccess)
        {
            var error = (DiscordMoniesError)priceMaybe.Error;
            await service.SendContextualAsync(
                error.Message,
                options: MessageOptions.Ephemeral);
            return;
        }

        var price = priceMaybe.Entity;

        if (!await TryPay(player, null, price))
        {
            await service.SendContextualAsync(
                Strings.BalanceTooLow("You do not have enough funds to develop the requested properties", player.Money, price),
                options: MessageOptions.Ephemeral);
            return;
        }

        // Everything is okay!

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"Development on {state.Board.GroupNames[colorSet.Index]}")
            .WithDescription($"Development on {state.Board.GroupNames[colorSet.Index]} has been completed by **{player}**. The changes are as follows:")
            .WithImageUrl("attachment://board.png");

        foreach (var (target, index) in targets.Take(spaces.Count).Select((x, i) => (x, i)))
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

        await service.SendContextualEmbedAsync(embedBuilder.Build().Get(), options: new()
        {
            Attachments = new([renderer.RenderToRemoraAttachment(spaces.Select(space => new HighlightedSpace(space, space.GroupColorOrDefault(PrettyColors.Green))))])
        });
    }

    private Result<int> EnsureDevelopmentIsPossible(IList<Space> spaces, IEnumerable<int> targets, int? housesRemainingOpt = null)
    {
        var usableTargets = targets.Take(spaces.Count).ToList();

        // Ensure no target is more than 1 away
        var min = usableTargets.Min();
        var max = usableTargets.Max();
        if (min < 0)
        {
            return new DiscordMoniesError("Unable to develop properties as per your request. You can have a minimum of 0 houses on any property.");
        }

        if (max > 5)
        {
            return new DiscordMoniesError("Unable to develop properties as per your request. You can have a maximum of 1 hotel on any property.");
        }

        if (min + 2 <= max)
        {
            return new DiscordMoniesError("Unable to develop properties as per your request. You must build evenly on all properties in the set (all properties must have 2 houses before you can build 3 houses on one property)");
        }

        var hotelsRemaining = state.Board.HotelsRemaining;
        var housesRemaining = housesRemainingOpt ?? state.Board.HousesRemaining;
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
                            return new DiscordMoniesError("No more hotels are available to service your request. (No changes were made to your properties.)");
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
                            return new DiscordMoniesError("No more houses are available to service your request. (No changes were made to your properties.)");
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
                            return new DiscordMoniesError("No more houses are available to service your request because demolishing a hotel requires 4 houses to be available. (No changes were made to your properties.)");
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

        return price;
    }

    public IEnumerable<Space> SpacesInColorSet(ColorSet colorSet)
    {
        return state.Board.Spaces.Where(x => x.Info is RoadSpace rs && rs.Group == colorSet.Index);
    }

    public IEnumerable<Space> SpacesInSet(PropertySpace ps) 
        => ps switch
        {
            RoadSpace rs => SpacesInColorSet(new(rs.Group)),
            TrainStationSpace ts => state.Board.Spaces.Where(x => x.Info is TrainStationSpace),
            UtilitySpace us => state.Board.Spaces.Where(x => x.Info is UtilitySpace),
            _ => throw new ArgumentException("Unexpected space type.", nameof(ps))
        };

    public async Task Mortgage(GamePlayer player, Space space)
    {
        if (space.Owner != player)
        {
            await service.SendContextualAsync($"Unable to mortgage **{space}** because you don't own it.");
            return;
        }

        if (space.BuiltHouses > 0)
        {
            await service.SendContextualAsync($"Unable to mortgage **{space}** because you have developed buildings on it.");
            return;
        }

        if (space.Info is RoadSpace rs)
        {
            if (SpacesInColorSet(new(rs.Group)).Any(property => property.BuiltHouses > 0))
            {
                await service.SendContextualAsync($"Unable to mortgage **{space}** because you have developed buildings on other spaces in the same color set.");
                return;
            }
        }

        if (space.BuiltHouses == 0)
        {
            await service.SendContextualAsync($"Mortgaging property {space}...");
            
            space.BuiltHouses = -1;
            await TryPay(null, player, space.TitleDeed!.MortgageValue);

            await service.SendEmbedAsync(state.ThreadId, Embeds.Mortgage(space));
        }
        else
        {
            if (!await TryPay(player, null, space.TitleDeed!.UnmortgageValue))
            {
                await service.SendContextualAsync(Strings.BalanceTooLow($"You do not have enough funds to unmortgage **{space}**", player.Money,
                        space.TitleDeed.UnmortgageValue));
            }
            
            await service.SendContextualAsync($"Unmortgaging mortgaged property {space}...");
            space.BuiltHouses = 0;

            await service.SendEmbedAsync(state.ThreadId, Embeds.Unmortgage(space));
        }
    }

    public async Task DropPlayer(GamePlayer player)
    {
        var transferredAssets = state.CurrentlyResolvingMatter is PaymentMatter matter ? matter.To : null;
        var transferredName = transferredAssets?.DisplayName ?? "the bank";
        
        // Transfer all of player's assets to transferredAssets
        List<string> billOfMaterials = [];
        
        // Use TryPay because Pay will queue up a payment
        await TryPay(player, transferredAssets, player.Money, true);
        billOfMaterials.Add($"{player.Money.MoneyString()} {Emoji.ArrowRight} **{transferredName}**");

        List<IOwnable> playerAssets = [];
        playerAssets.AddRange(state.Board.Spaces.Where(space => space.Owner == player));
        if (state.Board.GambleCards.JailCardOwner == player)
        {
            playerAssets.Add(state.Board.GambleCards);
        }
        if (state.Board.TreasureCards.JailCardOwner == player)
        {
            playerAssets.Add(state.Board.TreasureCards);
        }
        
        foreach (var ownable in playerAssets)
        {
            var unresolvedMatter = ownable.GiveTo(transferredAssets, state.Players);
            if (unresolvedMatter is not null)
            {
                state.UnresolvedMatters.Add(unresolvedMatter);
            }

            billOfMaterials.Add($"**{ownable.ToString()}** {Emoji.ArrowRight} **{transferredName}**");
        }
        
        // Remove the player from the game
        // Advance the player pointer if necessary
        var playerAdvanceRequired = state.CurrentPlayer == player && state.CurrentlyResolvingMatter is DiceRollMatter;
        if (state.CurrentPlayer == player)
        {
            state.Dice.Reset();
            
            // Move to the previous player so that the next turn is taken by the next player
            // because moving to the next turn increments the player pointer
            state.CurrentPlayerIndex = (state.CurrentPlayerIndex + state.Players.Count - 1) % state.Players.Count;
        }
        
        var nextPlayer = state.CurrentPlayer;
        state.Players.Remove(player);
        state.CurrentPlayerIndex = state.Players.IndexOf(nextPlayer);

        // Clear out all pending unresolved matters
        foreach (var unresolvedMatter in state.UnresolvedMatters.Where(x => x.Player == player).ToList())
        {
            state.UnresolvedMatters.Remove(unresolvedMatter);
        }
        
        await service.SendEmbedAsync(state.ThreadId, Embeds.Dropped(player, transferredAssets, billOfMaterials));
        
        if (state.Players.Count == 1)
        {
            // End of game!
            await service.SendEmbedAsync(state.ThreadId, Embeds.Victory(state.Players[0]),
                options: new()
                {
                    Attachments = new([renderer.RenderToRemoraAttachment([])]),
                });
            gameService.CloseGame(state);
            return;
        }
        
        // Advance the turn if the current player is waiting to roll so that the next player gets the prompt
        if (playerAdvanceRequired)
        {
            state.CurrentPlayerIndex -= 1;
            await ProcessNextUnresolvedMatter();
            return;
        }

        // Process the next unresolved matter if the current player is declaring bankruptcy to continue the game
        if (state.CurrentlyResolvingMatter is PaymentMatter pm && pm.Player == player)
        {
            await ProcessNextUnresolvedMatter();
        }
    }

    public async Task TakeCard()
    {
        var matter = (PayOrGambleMatter) state.CurrentlyResolvingMatter;
        await DrawCard(matter.CardStack);
        await ProcessNextUnresolvedMatter();
    }

    public async Task TakeCardPay()
    {
        var matter = (PayOrGambleMatter) state.CurrentlyResolvingMatter;
        if (!await TryPay(matter.Player, null, matter.PayValue))
        {
            await service.SendContextualAsync(
                Strings.BalanceTooLow("You do not have enough funds to do that", matter.Player.Money, matter.PayValue), 
                options: MessageOptions.Ephemeral);
            return;
        }

        await ProcessNextUnresolvedMatter();
    }

    public async Task MortgageTransferUnmortgage()
    {
        var matter = (MortgageTransferMatter)state.CurrentlyResolvingMatter;
        
        if (!await TryPay(matter.Player, null, matter.Space.TitleDeed!.UnmortgageValue))
        {
            await service.SendContextualAsync(Strings.BalanceTooLow($"You do not have enough funds to unmortgage **{matter.Space}**", matter.Player.Money,
                matter.Space.TitleDeed.UnmortgageValue));
            return;
        }
            
        await service.SendContextualAsync($"Unmortgaging mortgaged property {matter.Space}...");
        matter.Space.BuiltHouses = 0;

        await service.SendEmbedAsync(state.ThreadId, Embeds.Unmortgage(matter.Space));
        await ProcessNextUnresolvedMatter();
    }

    public async Task MortgageTransferMortgage()
    {
        var matter = (MortgageTransferMatter)state.CurrentlyResolvingMatter;
        await Pay(matter.Player, null, (int)(matter.Space.TitleDeed!.MortgageValue * 0.1));
        
        await service.SendContextualAsync($"Keeping mortgaged property {matter.Space} mortgaged...");
        await ProcessNextUnresolvedMatter();
    }

    public async Task AuctionDrop()
    {
        var player = state.GetById(context.GetUserId());
        var matter = (AuctionMatter) state.CurrentlyResolvingMatter;
        
        if (!context.Interaction.Message.HasValue)
        {
            await service.SendContextualAsync("Withdrawing from the auction...");
        }

        if (matter.BiddingPlayers.Count > 1)
        {
            var playerIndex = matter.BiddingPlayers.IndexOf(player);
            matter.Player = matter.BiddingPlayers[(playerIndex + 1) % matter.BiddingPlayers.Count];
        }
        
        matter.BiddingPlayers.Remove(player);
        await service.SendAsync(state.ThreadId,
            $"**{player}** has withdrawn from the auction.");
        await ProcessCurrentlyUnresolvedMatter();
    }

    public async Task AuctionBid(int value)
    {
        var player = state.GetById(context.GetUserId());
        var matter = (AuctionMatter) state.CurrentlyResolvingMatter;

        if (player.Money < matter.HighestBid + value)
        {
            await service.SendContextualAsync(Strings.BalanceTooLow($"You can't afford that bid", player.Money,
                matter.HighestBid + value));
            return;
        }
        
        if (!context.Interaction.Message.HasValue)
        {
            await service.SendContextualAsync($"Bidding {value.MoneyString()}...");
        }

        matter.HighestBid += value;
        matter.HighestBidder = player;
        
        await service.SendAsync(state.ThreadId,
            $"**{player}** bid {matter.HighestBid.MoneyString()} (+{value.MoneyString()})");
        
        var playerIndex = matter.BiddingPlayers.IndexOf(player);
        matter.Player = matter.BiddingPlayers[(playerIndex + 1) % matter.BiddingPlayers.Count];
        await ProcessCurrentlyUnresolvedMatter();
    }

    public IList<RecoupFundsOperation> TryRecoupFunds(GamePlayer player)
    {
        var operations = new List<RecoupFundsOperation>();

        var remainingDebt = -player.Money;
        var mortgageableSpaces = state.Board.Spaces.Where(s => s.Owner == player && !s.IsMortgaged);

        var nonColorSetSpaces = mortgageableSpaces
            .Where(s => ColorSetOwner((PropertySpace)s.Info) is null)
            .OrderByDescending(s => s.TitleDeed!.MortgageValue)
            .ToList();

        MortgageProperties(nonColorSetSpaces);

        if (remainingDebt <= 0)
        {
            return operations;
        }

        var colorSetSpaces = mortgageableSpaces
            .Where(s => ColorSetOwner((PropertySpace)s.Info) is not null)
            .Where(s => SpacesInSet((PropertySpace)s.Info).All(s => s.BuiltHouses <= 0))
            .OrderByDescending(s => s.TitleDeed!.MortgageValue)
            .ToList();

        MortgageProperties(colorSetSpaces);

        if (remainingDebt <= 0)
        {
            return operations;
        }

        var ownedSets =
            mortgageableSpaces
            .Where(s => ColorSetOwner((PropertySpace)s.Info) is not null)
            .Where(s => s.Info is RoadSpace)
            .GroupBy(s => ((RoadSpace)s.Info).Group)
            .Select(g => g.ToList())
            .ToList();

        int availableHouses = state.Board.HousesRemaining;

        int TotalValue(IList<Space> set)
        {
            var result = EnsureDevelopmentIsPossible(set, [0, 0, 0], availableHouses);

            if (result.IsSuccess)
            {
                return -result.Entity + set.Select(a => a.TitleDeed!.MortgageValue).Sum();
            }

            var builtHouses = set.Select(s => s.BuiltHouses).ToList();

            switch (builtHouses.Count(s => s == 5))
            {
                case 1:
                    // If we cannot sell all developments, and we only have one hotel,
                    // there is no way to sell any developments on this set
                    break;

                case 2:
                    builtHouses[builtHouses.IndexOf(5)] = 4;
                    result = EnsureDevelopmentIsPossible(set, builtHouses, availableHouses);
                    if (result.IsSuccess)
                    {
                        return -result.Entity;
                    }

                    break;

                case 3:
                    builtHouses[builtHouses.IndexOf(5)] = 4;

                    int secondHotelIndex = builtHouses.IndexOf(5);
                    builtHouses[secondHotelIndex] = 4;

                    result = EnsureDevelopmentIsPossible(set, builtHouses, availableHouses);
                    if (result.IsSuccess)
                    {
                        return -result.Entity;
                    }

                    builtHouses[secondHotelIndex] = 5;
                    result = EnsureDevelopmentIsPossible(set, builtHouses, availableHouses);
                    if (result.IsSuccess)
                    {
                        return -result.Entity;
                    }

                    break;
            }

            return 0;
        }

        while (remainingDebt > 0 && ownedSets.Count > 0)
        {
            var (spaces, totalValue) =
                ownedSets
                .Select(s => (s, TotalValue: TotalValue(s)))
                .MinBy(s => s.TotalValue);

            ownedSets.Remove(spaces);

            var propertyCounts = spaces.Select(s => s.BuiltHouses).ToList();

            while (remainingDebt > 0 && totalValue != 0)
            {
                var tallestCount = propertyCounts.Max();
                var firstMaxIndex = propertyCounts.IndexOf(tallestCount);
                var space = spaces[firstMaxIndex];

                int recoupedCost;
                if (tallestCount == 0)
                {
                    operations.Add(new MortgageProperty(space));
                    recoupedCost = space.TitleDeed!.MortgageValue;
                }
                else
                {
                    operations.Add(new SellImprovement(space));
                    recoupedCost = (tallestCount == 5 ? space.TitleDeed!.HotelCost : space.TitleDeed!.HouseCost) / 2;

                    if (tallestCount == 5)
                    {
                        availableHouses -= 4;
                    } 
                    else
                    {
                        availableHouses++;
                    }

                }

                propertyCounts[firstMaxIndex]--;

                totalValue -= recoupedCost;
                remainingDebt -= recoupedCost;
            }
        }

        if (remainingDebt <= 0)
        {
            return operations;
        }

        return [];

        void MortgageProperties(List<Space> candidateSpaces)
        {
            while (remainingDebt > 0 && candidateSpaces.Count > 0)
            {
                // Find the cheapest space which eliminates the debt. If there aren't any, just pick the most expensive space and continue
                var mortgageSpace = candidateSpaces.FindLast(s => s.TitleDeed!.MortgageValue >= remainingDebt) ?? candidateSpaces.First();

                candidateSpaces.Remove(mortgageSpace);
                operations.Add(new MortgageProperty(mortgageSpace));
                remainingDebt -= mortgageSpace.TitleDeed!.MortgageValue;
            }
        }
    }

    public async Task AutoSell(int currentBalance)
    {
        var matter = (PaymentMatter)state.CurrentlyResolvingMatter;
        if (state.CurrentPlayer.Money != currentBalance)
        {
            await RecalculateAutoSell();
            return;
        }

        if (matter.AutoSell is null)
        {
            await RecalculateAutoSell();
            return;
        }

        // Ensure autosell is still valid
        List<Space> mortgagedSpaces = [];
        Dictionary<Space, int> housesOnSpace = new();
        var remainingHouses = state.Board.HousesRemaining;
        
        foreach (var operation in matter.AutoSell!)
        {
            switch (operation)
            {
                case MortgageProperty mortgage:
                    if (mortgagedSpaces.Contains(mortgage.Space) || mortgage.Space.IsMortgaged)
                    {
                        // Property is already mortgaged
                        await RecalculateAutoSell();
                        return;
                    }
                    
                    mortgagedSpaces.Add(mortgage.Space);
                    break;

                case SellImprovement sell:
                    var ok = housesOnSpace.TryGetValue(sell.Space, out var houses);
                    if (!ok) houses = sell.Space.BuiltHouses;

                    if (houses == 0)
                    {
                        // No more houses to sell
                        await RecalculateAutoSell();
                        return;
                    }

                    if (houses == 5)
                    {
                        // 4 houses less because we're selling a hotel
                        remainingHouses -= 4;
                    }
                    else
                    {
                        // Selling one house to the bank
                        remainingHouses += 1;
                    }

                    if (remainingHouses < 0)
                    {
                        // Oversold on houses
                        await RecalculateAutoSell();
                        return;
                    }
                    
                    houses -= 1;
                    housesOnSpace[sell.Space] = houses;
                    break;
            }
        }

        var moneyEarned = 0;

        foreach (var operation in matter.AutoSell!)
        {
            switch (operation)
            {
                case MortgageProperty mortgage:
                    mortgage.Space.BuiltHouses = -1;
                    moneyEarned += mortgage.Space.TitleDeed!.MortgageValue;
                    break;

                case SellImprovement sell:
                    sell.Space.BuiltHouses -= 1;
                    var titleDeed = sell.Space.TitleDeed!;
                    moneyEarned += (sell.Space.BuiltHouses == 4 ? titleDeed.HotelCost : titleDeed.HouseCost) / 2;
                    // TODO: send message
                    break;
            }
        }
        
        await TryPay(null, state.CurrentPlayer, moneyEarned);
        await service.SendContextualEmbedAsync(Embeds.AutoSellComplete(state.CurrentPlayer, matter.AutoSell, moneyEarned));
    }

    private async Task RecalculateAutoSell()
    {
        var matter = (PaymentMatter)state.CurrentlyResolvingMatter;
        var operations = TryRecoupFunds(state.CurrentPlayer);
        if (operations.Count == 0)
        {
            matter.AutoSell = null;
            await service.SendContextualEmbedAsync(Embeds.AutoSellUnable(), options: new()
            {
                MessageComponents = new([
                    new ActionRowComponent([
                        Buttons.DeclareBankruptcy()
                    ])
                ])
            });
        }
        else
        {
            matter.AutoSell = operations;
            await service.SendContextualEmbedAsync(Embeds.NewAutoSellSituation(operations), options: new()
            {
                MessageComponents = new([
                    new ActionRowComponent([
                        Buttons.AutoSell(state.CurrentPlayer.Money)
                    ])
                ])
            });
        }
    }
}

public abstract record RecoupFundsOperation;
public sealed record MortgageProperty(Space Space) : RecoupFundsOperation;
public sealed record SellImprovement(Space Space) : RecoupFundsOperation;
