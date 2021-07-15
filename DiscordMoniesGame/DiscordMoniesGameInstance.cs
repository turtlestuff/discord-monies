using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Leisure;

namespace DiscordMoniesGame
{
    public sealed partial class DiscordMoniesGameInstance : GameInstance
    {
        enum Waiting
        {
            ForNothing,
            ForRentPay,
            ForTaxPay,
            ForAuctionOrBuyDecision,
            ForAuctionToFinish,
            ForOtherJailDecision,
            ForCardPay,
            ForRepairsPay
        }

        [Flags]
        public enum JailCards
        {
            None = 0,
            Chance = 1,
            Chest = 2
        }

        public record PlayerState(int Money, int Position, int JailStatus, JailCards JailCards, System.Drawing.Color Color);
        // JailStatus: -1 if out of jail, 0-3 if in jail, counting the consecutive turns of double attempts.

        int jailRoll;

        readonly int originalPlayerCount;
        readonly ConcurrentDictionary<IUser, PlayerState> pSt = new(DiscordComparers.UserComparer);
        Board board = default!;
        IUser currentPlr;

        int round = 1;
        int continuousRolls;
        bool rollAgain = false;

        readonly BoardRenderer boardRenderer = new();

        Waiting waiting = Waiting.ForNothing;
        int lastRoll;

        ConcurrentDictionary<IUser, int?>? auctionState;
        IUser? currentAuctionPlayer;
        int? auctionSpace;

        IUser? chanceJailFreeCardOwner;
        IUser? chestJailFreeCardOwner;

        int? cardOwe;
        int? repairsOwe;

        public DiscordMoniesGameInstance(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators)
            : base(id, client, players, spectators)
        {
            originalPlayerCount = players.Length;
            RegisterCommands();
            currentPlr = Players[Random.Shared.Next(Players.Length)];
            Closing += (_, _) => boardRenderer.Dispose();
        }


        public override async Task Initialize()
        {
            var asm = GetType().Assembly;

            using var boardStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.board.json")!;
            using var titleStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.titledeeds.json")!;
            using var chestStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.communitychest.json")!;
            using var chanceStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.chance.json")!;

            board = await Board.BoardFromJson(boardStream, titleStream, chestStream, chanceStream);

            var shuffledColors = Colors.ColorList.OrderBy(_ => Random.Shared.Next()).ToArray();

            for (var i = 0; i < Players.Length; i++)
            {
                if (!pSt.TryAdd(Players[i],
                    new PlayerState(board.StartingMoney, 00, -1, JailCards.None, shuffledColors[i].Value)))
                    throw new Exception("Something very wrong happened Initializing");
            }

            var embed = new EmbedBuilder()
            {
                Title = "Ðiscord Monies",
                Description = $"The game has started! Every player has been given {board.StartingMoney.MoneyString()}.",
                Fields = new()
                {
                    new()
                    {
                        IsInline = false,
                        Name = "Playing Order",
                        Value = PrettyOrderedPlayers(currentPlr, true, true, true)
                    }
                },
                Color = Color.Green
            };
            await SendBoard(Users, embed);
        }

        public override async Task OnMessage(IUserMessage msg, int pos)
        {
            try
            {
                using (msg.Channel.EnterTypingState())
                {
                    var msgContent = msg.Content[pos..];

                    if (await TryHandleCommand(msgContent, msg))
                        return;

                    //Message hasn't matched any commands, proceed to send as chat message...
                    var chatMessage =
                        $"**{msg.Author.Username}{(Spectators.Contains(msg.Author, DiscordComparers.UserComparer) ? " (Spectator)" : "")}**: {msgContent}";
                    if (chatMessage.Length <= 2000)
                    {
                        // ...except if it's too large!
                        await this.BroadcastExcluding(chatMessage, exclude: msg.Author);
                        await msg.AddReactionAsync(new Emoji("💬"));
                    }
                }
            }
            catch (Exception ex)
            {
                await msg.Author.SendMessageAsync($"Command failed: {ex.Message}");
                Console.Error.WriteLine(ex);
            }
        }

        async Task HandlePlayerLand(int position)
        {
            if (board.Spaces[position] is DrawCardSpace dcs)
            {
                var card = board.DrawLuckCard(dcs.Type);
                var parts = card.Command.Split(' ');
                if (parts[0] == "jailfree")
                {

                    if ((parts[1] == "chance" && chanceJailFreeCardOwner is not null) ||
                        (parts[1] == "chest" && chestJailFreeCardOwner is not null))
                    {
                        //Draw a new card and try again!
                        await HandlePlayerLand(position);
                        return;
                    }
                }

                var humanReadableDescription = Regex.Replace(card.Description, "{(moneyValue|boardSpace) ([^}]+)}", match =>
                {
                    var type = match.Groups[1].Value;
                    var value = match.Groups[2].Value;
                    bool isInt = int.TryParse(value, out var val);

                    if (type == "moneyValue")
                    {
                        if (isInt)
                        {
                            return val.MoneyString();
                        }
                        else
                        {
                            if (value == "goSpace") return board.PassGoValue.MoneyString();
                            return value;
                        }
                    }
                    else
                    {
                        return $"{board.Spaces[val].Name} ({val.LocString()})";
                    }
                } );

                var cardType = dcs.Type == CardType.Chance ? "Gamble" : "Dubious Treasure";

                var e = new EmbedBuilder()
                {
                    Title = cardType,
                    Description = $"**{currentPlr.Username}** draws a {cardType} card, and it goes as follows...",
                    Color = Color.Gold,
                    Fields = new()
                    {
                        new()
                        {
                            IsInline = true,
                            Name = $"{cardType} card",
                            Value = humanReadableDescription
                        }
                    }
                }.Build();
                await this.Broadcast("", embed: e);

                switch (parts[0])
                {
                    case "arrest":
                        await SendToJail(currentPlr);
                        break;
                    case "give":
                        await TryTransfer(int.Parse(parts[1]), null, currentPlr);
                        break;
                    case "pay":
                        if (!await TryTransfer(int.Parse(parts[1]), currentPlr, null))
                        {
                            cardOwe = int.Parse(parts[1]);
                            waiting = Waiting.ForCardPay;

                            var e1 = new EmbedBuilder()
                            {
                                Title = "You are in arrears!",
                                Description = $"Use `paycard` once you have raised enough funds to contine.",
                                Color = Color.Red
                            }.Build();
                            await currentPlr.SendMessageAsync("", embed: e1);

                            return;
                        }
                        break;
                    case "repairs":
                        {
                            var houseValue = int.Parse(parts[1]);
                            var hotelValue = int.Parse(parts[2]);

                            var total = board.Spaces.Aggregate(0, (currentTotal, space) =>
                            {
                                if (space is RoadSpace rs)
                                {
                                    if (rs.Owner?.Id != currentPlr.Id) return currentTotal;
                                    if (rs.Houses == 5) return currentTotal + hotelValue;
                                    return currentTotal + rs.Houses * houseValue;
                                }
                                else
                                {
                                    return currentTotal;
                                }
                            });

                            if (!await TryTransfer(total, currentPlr, null))
                            {
                                repairsOwe = total;
                                waiting = Waiting.ForCardPay;

                                var e1 = new EmbedBuilder()
                                {
                                    Title = "You are in arrears!",
                                    Description = $"Use `payrepairs` once you have raised enough funds to contine.",
                                    Color = Color.Red
                                }.Build();
                                await currentPlr.SendMessageAsync("", embed: e1);

                                return;
                            }

                            break;
                        }
                    case "warp":
                        var move = await MovePlayer(currentPlr, int.Parse(parts[1]));
                        await HandlePlayerLand(move); 
                        return;
                    case "warprel":
                        var move1 = await MovePlayerRelative(currentPlr, int.Parse(parts[1]), false);
                        await HandlePlayerLand(move1); 
                        return;
                    case "jailfree":
                        if (parts[1] == "chance")
                        {
                            chanceJailFreeCardOwner = currentPlr;
                        }
                        else
                        {
                            chestJailFreeCardOwner = currentPlr;
                        }
                        break;
                    default:
                        break;
                }
            }

            if (board.Spaces[position] is TaxSpace ts)
            {
                waiting = Waiting.ForTaxPay;
                var e = new EmbedBuilder()
                {
                    Title = "Tax",
                    Description = $"{ts.Name} is {ts.Value.MoneyString()}. Please pay this with the `paytax` command.",
                    Color = Color.Red
                }.Build();
                await currentPlr.SendMessageAsync("", embed: e);
                return;
            }
            if (board.Spaces[position] is PropertySpace ps)
            {
                if (ps.Owner is null)
                {
                    await currentPlr.SendMessageAsync("", embed: board.CreateTitleDeedEmbed(position));
                    waiting = Waiting.ForAuctionOrBuyDecision;
                    var era = new EmbedBuilder()
                    {
                        Title = "Unowned Property",
                        Description = $"This property is not owned. You must choose between buying the property for its listed price ({ps.Value.MoneyString()})" +
                        $" with `buythis` or holding an auction with `auctionthis`.",
                        Color = board.GroupColorOrDefault(ps, Color.Gold)
                    }.Build();
                    await currentPlr.SendMessageAsync("", embed: era);
                    return;
                }

                if (ps.Owner.Id == currentPlr.Id)
                {
                    await AdvanceRound();
                    return; // no need to pay rent!
                }

                if (ps.Mortgaged)
                {
                    var e1 = new EmbedBuilder()
                    {
                        Title = "Rent",
                        Description = $"You landed on a mortgaged property, therefore you do not need to pay any rent.",
                        Color = board.GroupColorOrDefault(ps, Color.Red)
                    }.Build();
                    await currentPlr.SendMessageAsync("", embed: e1);
                    await AdvanceRound();
                    return;
                }

                //TODO: Pay rent automatically where possible
                var rent = board.CalculateRentFor(position);
                if (board.Spaces[position] is UtilitySpace)
                    rent *= lastRoll;

                waiting = Waiting.ForRentPay;
                var e = new EmbedBuilder()
                {
                    Title = "Rent",
                    Description = $"The rent for the square you have landed on is {rent.MoneyString()}. Please pay this with the `payrent` command.",
                    Color = board.GroupColorOrDefault(ps, Color.Red)
                }.Build();
                await currentPlr.SendMessageAsync("", embed: e);
                return;
            }
            // nothing happens!
            await AdvanceRound();
        }

        IUser NextPlayer(IUser plr) => Players[(Players.IndexOf(plr, DiscordComparers.UserComparer) + 1) % Players.Length];

        IUser[] OrderedPlayers(IUser start)
        {
            var index = Players.IndexOf(start, DiscordComparers.UserComparer);
            var playersArray = Players.ToArray();
            return playersArray[index..].Concat(playersArray[..index]).ToArray();
        }

        string PrettyOrderedPlayers(IUser start, bool index, bool bold, bool color)
        {
            string Do(IUser p, int x)
            {
                var i = index ? $"{x + 1}: " : "";
                var b = bold && x == 0 ? "**" : "";
                var c = color ? $" ({Colors.NameOfColor(pSt[p].Color)})" : "";
                return b + i + p.Username + c + b;
            }
            return string.Join('\n', OrderedPlayers(start).Select(Do));
        }

        async Task AdvanceRound()
        {
            waiting = Waiting.ForNothing;
            if (rollAgain)
            {
                rollAgain = false;
                var ie = new EmbedBuilder()
                {
                    Title = "Doubles 🎲 🎲", // two dice emoji
                    Description = $"Since **{currentPlr.Username}** had rolled doubles, they may roll another time!",
                    Color = Color.Green
                };

                await SendBoard(Users, ie);
                return;
            }
            continuousRolls = 0;
            round++;
            currentPlr = NextPlayer(currentPlr);
            var embed = new EmbedBuilder()
            {
                Title = "Next Round",
                Description = $"Round: {round}\nPlayer: **{currentPlr.Username}** @ " +
                (pSt[currentPlr].JailStatus == -1 ? $"{board.Spaces[pSt[currentPlr].Position].Name} ({pSt[currentPlr].Position.LocString()})" : "Jail"),
                Color = Color.Gold
            };
            await SendBoard(Users, embed);
        }

        async Task SendToJail(IUser player)
        {
            rollAgain = false;
            pSt[player] = pSt[player] with { JailStatus = 0, Position = board.VisitingJailPosition };
            var embed = new EmbedBuilder()
            {
                Title = "Jail 🚔", //oncoming police car emoji
                Description = $"**{player.Username}** has been sent to jail.\n" +
                $"They can try rolling doubles up to 3 times, pay a fine of {board.JailFine.MoneyString()} with `bail`, or use a Get out of Jail Free card with `usejailcard`.",
                Color = Color.Red
            }.Build();

            await this.Broadcast("", embed: embed);
        }

        async Task SendBoard(IEnumerable<IUser> users, EmbedBuilder? embed = null)
        {
            try
            {
                using var bmp = boardRenderer.Render(Players, pSt, board);
                using var memStr = new MemoryStream();
                bmp.Save(memStr, System.Drawing.Imaging.ImageFormat.Png);
                foreach (var u in users)
                {
                    using var clone = new MemoryStream();
                    memStr.Position = 0;
                    await memStr.CopyToAsync(clone);
                    clone.Position = 0;
                    if (embed is not null)
                    {
                        embed.ImageUrl = "attachment://board.png";
                        await u.SendFileAsync(clone, "board.png", embed: embed.Build());
                    }
                    else
                    {
                        await u.SendFileAsync(clone, "board.png");
                    }
                }
            }
            catch (ObjectDisposedException e)
            {
                await this.Broadcast("Something went wrong trying to send the board. Don't worry! The turn has gone forward! Use `board` to get a new board image and `status` to see the current player.");
                Console.Error.WriteLine(e);
            }
        }

        async Task<int> MovePlayer(IUser player, int position, bool passGoBonus = true)
        {
            if (position < pSt[player].Position && passGoBonus)
            {
                await GiveMoney(player, board.PassGoValue);
                var embed = new EmbedBuilder()
                {
                    Title = "Pass Go Bonus!",
                    Description = $"**{player.Username}** has gotten {board.PassGoValue.MoneyString()} for passing GO!"
                }.Build();
                await this.Broadcast("", embed: embed);
            }
            pSt[player] = pSt[player] with { Position = position };
            return position;
        }

        async Task<int> MovePlayerRelative(IUser player, int amount, bool passGoBonus = true)
        {
            var position = (pSt[player].Position + amount) % board.Spaces.Length;
            return await MovePlayer(player, position, passGoBonus);
        }

        Color PlayerColor(IUser player) => pSt[player].Color.ToDiscordColor();

        async Task<bool> TryTransfer(int amount, IUser? payer = null, IUser? reciever = null)
        {
            if (payer is not null)
            {
                if (amount > pSt[payer].Money)
                {
                    await payer.SendMessageAsync($"You do not have enough money to make this transaction. (You have: {pSt[payer].Money.MoneyString()}. Required amount: {amount.MoneyString()})");
                    return false;
                }
                pSt[payer] = pSt[payer] with { Money = pSt[payer].Money - amount };
                await payer.
                    SendMessageAsync($"You have transferred {amount.MoneyString()} to {reciever?.Username ?? "the bank"}. Your balance is now {pSt[payer].Money.MoneyString()}.");
            }

            if (reciever is not null)
                await GiveMoney(reciever, amount, payer?.Username ?? "the bank");

            await this.BroadcastExcluding($"💸 {amount.MoneyString()}: **{payer?.Username ?? "Bank"}** ➡️ **{reciever?.Username ?? "Bank"}**",
                exclude: new[] { reciever, payer }.Where(x => x is not null).Cast<IUser>().ToArray());
            return true;
        }

        async Task GiveMoney(IUser reciever, int amount, string? giver = null)
        {
            pSt[reciever] = pSt[reciever] with { Money = pSt[reciever].Money + amount };
            if (giver is not null)
                await reciever.SendMessageAsync($"You have recieved {amount.MoneyString()} from **{giver}**. Your balance is now {pSt[reciever].Money.MoneyString()}.");
        }

        async Task<bool> TryBuyProperty(IUser player, int pos, int amount)
        {
            var space = board.Spaces[pos];
            if (space is not PropertySpace ps)
            {
                await player.SendMessageAsync($"\"{space.Name}\" is not a property.");
                return false;
            }
            if (ps.Owner is not null)
            {
                await player.SendMessageAsync($"\"{space.Name}\" is already owned by a player.");
                return false;
            }
            if (await TryTransfer(amount, player))
            {
                board.Spaces[pos] = ps with { Owner = player };
                var embed = new EmbedBuilder()
                {
                    Title = "Property Obtained",
                    Description = $"**{player.Username}** has obtained **{ps.Name}** ({pos.LocString()}) for {amount.MoneyString()}!",
                    Color = board.GroupColorOrDefault(ps, Color.Green)
                }.Build();
                await this.Broadcast("", embed: embed);
                return true;
            }
            return false;
        }

        async Task<bool> TryTakeJailFreeCard(IUser player)
        {
            if (chanceJailFreeCardOwner?.Id == player.Id)
            {
                chanceJailFreeCardOwner = null;
                return true;
            }
            if (chestJailFreeCardOwner?.Id == player.Id)
            {
                chestJailFreeCardOwner = null;
                return true;
            }
            await player.SendMessageAsync($"You do not have a Get Out of Jail Free card.");
            return false;
        }

        async Task NextBid(int bid)
        {
            if (auctionState is null || !auctionSpace.HasValue || currentAuctionPlayer is null)
            {
                await FinalizeAuction();
                return;
            }

            auctionState[currentAuctionPlayer] = bid;

            string bidString;
            if (bid != -1)
                bidString = $"**{currentAuctionPlayer.Username}** has bid {bid.MoneyString()}.";
            else
                bidString = $"**{currentAuctionPlayer.Username}** has skipped.";

            if (!auctionState.Values.Contains(null) &&
                auctionState.Values.Count(x => x == -1) + 1 >= auctionState.Count)
            // this checks if all, or 1 less than the total has skipped
            {
                var embed = new EmbedBuilder()
                {
                    Title = "Auction",
                    Description = bidString,
                    Color = board.GroupColorOrDefault(board.Spaces[auctionSpace.Value])
                }.Build();
                await this.Broadcast("", embed: embed);
                await FinalizeAuction();
                return;
            }

            var nextPlayer = NextPlayer(currentAuctionPlayer);
            while (auctionState[nextPlayer] == -1)
                nextPlayer = NextPlayer(nextPlayer);


            if (nextPlayer.Id == currentAuctionPlayer.Id)
            {
                var embed = new EmbedBuilder()
                {
                    Title = "Auction",
                    Description = bidString,
                    Color = board.GroupColorOrDefault(board.Spaces[auctionSpace.Value])
                }.Build();
                await this.Broadcast("", embed: embed);
                await FinalizeAuction();
            }
            else
            {
                var embed = new EmbedBuilder()
                {
                    Title = "Auction",
                    Description = $"{bidString}\n**{nextPlayer.Username}** is next to bid.",
                    Color = board.GroupColorOrDefault(board.Spaces[auctionSpace.Value])
                }.Build();
                await this.Broadcast("", embed: embed);
                currentAuctionPlayer = nextPlayer;
            }

        }
        async Task FinalizeAuction()
        {
            if (auctionState is null || !auctionSpace.HasValue || currentAuctionPlayer is null)
            {
                await this.Broadcast("Something has gone wrong. Property has been sent back to the bank.");
            }
            else
            {
                var space = auctionSpace.Value;
                var maxBid = auctionState.Values.Max();
                if (maxBid.HasValue)
                {
                    if (maxBid == -1)
                    {
                        var e = new EmbedBuilder()
                        {
                            Title = "Auction",
                            Description = "All players have skipped. Property has been sent back to the bank.",
                            Color = board.GroupColorOrDefault(board.Spaces[space])
                        }.Build();
                        await this.Broadcast("", embed: e);
                    }
                    else
                    {
                        var maxBidPlayer = auctionState.First(x => x.Value == maxBid).Key;
                        if (!await TryBuyProperty(maxBidPlayer, space, maxBid.Value))
                        {
                            await this.Broadcast("Something has gone wrong. Property has been sent back to the bank.");
                        }
                    }
                }
                else
                {
                    await this.Broadcast("Something has gone wrong. Property has been sent back to the bank.");
                }
            }
            auctionState = null;
            auctionSpace = null;
            currentAuctionPlayer = null;
            await AdvanceRound();
        }

        async Task<bool> TryDevelopSpace(IUser developer, int loc, bool demolish)
        {
            var space = board.Spaces[loc];
            var diff = demolish ? -1 : 1;
            var deed = board.TitleDeedFor(loc);
            if (space is not RoadSpace rs || rs.Owner?.Id != developer.Id)
            {
                await developer.SendMessageAsync("You can't develop this property.");
                return false;
            }

            if (board.IsEntireGroupOwned(rs.Group, out var spaces))
            {
                if (spaces.Any(space => space.Mortgaged))
                {
                    await developer.SendMessageAsync("You can't develop this property right now because other properties in this set are mortgaged.");
                    return false;
                }

                if (rs.Houses + diff > 5 || rs.Houses + diff < 0)
                {
                    if (demolish)
                    {
                        await developer.SendMessageAsync("You can't demolish anything here because there are no more houses to demolish.");
                    }
                    else
                    {
                        await developer.SendMessageAsync("You can't develop this property further because there is already a hotel on this property.");
                    }
                    return false;
                }

                //Checking whether new configuration will be valid
                int compareValue = demolish ? spaces.Max(x => x.Houses) : spaces.Min(x => x.Houses);

                if (rs.Houses == compareValue)
                {
                    if (demolish)
                    {
                        if (rs.Houses == 5)
                        {
                            if (!board.CanTakeHouse(4))
                            {
                                await developer.SendMessageAsync("You can't demolish this hotel because there are no more houses to purchase.");
                                return false;
                            }

                            await TryTransfer(deed.HotelCost / 2, null, developer);
                            board.ReturnHotel();
                            board.Spaces[loc] = rs with { Houses = 4 };

                            var embed1 = new EmbedBuilder()
                            {
                                Title = "Hotel Demolished",
                                Description = $"The hotel on **{rs.Name}** ({loc.LocString()}) has been demolished, leaving 4 houses there.",
                                Color = board.GroupColorOrDefault(rs)
                            }.Build();
                            await this.Broadcast("", embed: embed1);
                            return true;
                        }
                        await TryTransfer(deed.HouseCost / 2, null, developer);
                        board.ReturnHouse();
                        board.Spaces[loc] = rs with { Houses = rs.Houses - 1 };

                        var embed = new EmbedBuilder()
                        {
                            Title = "House Demolished",
                            Description = $"A house on **{rs.Name}** ({loc.LocString()}) has been demolished, leaving {rs.Houses} {((rs.Houses - 1) == 1 ? "house" : "houses")} there.",
                            Color = board.GroupColorOrDefault(rs)
                        }.Build();
                        await this.Broadcast("", embed: embed);

                        return true;
                    }
                    else // building
                    {
                        if (rs.Houses != 4)
                        {
                            if (!board.CanTakeHouse())
                            {
                                await developer.SendMessageAsync("You can't develop this property because there are no more houses to purchase.");
                                return false;
                            }
                            if (await TryTransfer(deed.HouseCost, developer, null))
                            {
                                board.Spaces[loc] = rs with { Houses = rs.Houses + 1 };

                                var embed = new EmbedBuilder()
                                {
                                    Title = "House Built",
                                    Description = $"Development on **{rs.Name}** ({loc.LocString()}) has resulted in a new house being built, " +
                                    $"for a total of {rs.Houses + 1} {((rs.Houses + 1) == 1 ? "house" : "houses")} on the property.",
                                    Color = board.GroupColorOrDefault(rs)
                                }.Build();
                                await this.Broadcast("", embed: embed);
                                return true;
                            }
                            board.ReturnHouse();
                            return false;
                        }
                        else if (rs.Houses == 4)
                        {
                            if (!board.CanTakeHotel())
                            {
                                await developer.SendMessageAsync("You can't develop this property because there are no more hotels to purchase.");
                                return false;
                            }
                            if (await TryTransfer(deed.HotelCost, developer, null))
                            {
                                board.ReturnHouse(4);
                                board.Spaces[loc] = rs with { Houses = 5 };

                                var embed = new EmbedBuilder()
                                {
                                    Title = "Hotel Built",
                                    Description = $"Development on **{rs.Name}** ({loc.LocString()}) has resulted in a new hotel being built.",
                                    Color = board.GroupColorOrDefault(rs)
                                }.Build();
                                await this.Broadcast("", embed: embed);
                                return true;
                            }
                            board.ReturnHotel();
                            return false;
                        }
                    }
                }
                else
                {
                    await developer.SendMessageAsync("You can't develop this property because developing it would cause the color set to be developed unevenly.");
                    return false;
                }
            }

            await developer.SendMessageAsync("You can't develop this property because you do not own its entire group yet.");
            return false;
        }

        void Close()
        {
            OnClosing();
        }

        void DropPlayer(IUser player)
        {
            OnDroppingUser(player);
        }
    }
}