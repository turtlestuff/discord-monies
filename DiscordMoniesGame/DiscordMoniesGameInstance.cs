using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
        }

        public record PlayerState(int Money, int Position, int JailStatus, bool GetOutOfJailCard, System.Drawing.Color Color);
        // JailStatus: -1 if out of jail, 0-3 if in jail, counting the consecutive turns of double attempts.

        readonly int originalPlayerCount;
        readonly ConcurrentDictionary<IUser, PlayerState> pSt = new(DiscordComparers.UserComparer);
        Board board = default!;
        IUser currentPlr;

        int round = 1;
        int continuousRolls;
        bool doubleTurn = false;

        readonly BoardRenderer boardRenderer = new();

        Waiting waiting = Waiting.ForNothing;
        int lastRoll;

        ConcurrentDictionary<IUser, int?>? auctionState;
        IUser? currentAuctionPlayer;
        int? auctionSpace;

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
                    new PlayerState(board.StartingMoney, 00, -1, false, shuffledColors[i].Value)))
                    throw new Exception("Something very wrong happened Initializing");
            }

            var embed = new EmbedBuilder()
            {
                Title = "First Round",
                Description = $"The game has started! Every player has been given {board.StartingMoney.MoneyString()}.",
                Fields = new()
                {
                    new()
                    {
                        IsInline = false, Name = "Playing Order",
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
                await this.BroadcastTo($"Command failed: {ex.Message}", players: msg.Author);
                Console.Error.WriteLine(ex);
            }
        }

        async Task HandlePlayerLand(int position)
        {
            if (board.Spaces[position] is TaxSpace ts)
            {
                waiting = Waiting.ForTaxPay;
                var e = new EmbedBuilder()
                {
                    Title = "Tax",
                    Description = $"{ts.Name} is {ts.Value.MoneyString()}. Please pay this with the `paytax` command.",
                    Color = Color.Red
                }.Build();
                await this.BroadcastTo("", embed: e, players: currentPlr);
                return;
            }
            if (board.Spaces[position] is PropertySpace ps)
            {
                if (ps.Mortgaged || ps.Owner == currentPlr)
                {
                    await AdvanceRound();
                    return; // no need to pay rent!
                }

                if (ps.Owner is null)
                {
                    waiting = Waiting.ForAuctionOrBuyDecision;
                    var era = new EmbedBuilder()
                    {
                        Title = "Unowned Property",
                        Description = $"This property is not owned. You must choose between buying the property for its listed price ({ps.Value.MoneyString()})" +
                        $" with `buythis` or holding an auction with `auctionthis`.",
                        Color = board.GroupColorOrDefault(ps, Color.Gold)
                    }.Build();
                    await this.BroadcastTo("", embed: era, players: currentPlr);
                    return;
                }

                waiting = Waiting.ForRentPay;
                var e = new EmbedBuilder()
                {
                    Title = "Rent",
                    Description = $"The rent for the square you have landed on is {board.CalculateRentFor(position).MoneyString()}. Please pay this with the `payrent` command.",
                    Color = board.GroupColorOrDefault(ps, Color.Red)
                }.Build();
                await this.BroadcastTo("", embed: e, players: currentPlr);
                return;
            }
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
            if (doubleTurn)
            {
                doubleTurn = false;
                var ie = new EmbedBuilder()
                {
                    Title = "Doubles 🎲 🎲", // two dice emoji
                    Description = $"Since {currentPlr.Username} had rolled doubles, they may roll another time!",
                    Color = Color.Green
                }.Build();
                await this.Broadcast("", embed: ie);
                return;
            }
            continuousRolls = 0;
            round++;
            currentPlr = NextPlayer(currentPlr);
            var embed = new EmbedBuilder()
            {
                Title = "Next Round",
                Description = $"Current Round: {round}\nPlayer: **{currentPlr.Username}** @ " +
                (pSt[currentPlr].JailStatus == -1 ? pSt[currentPlr].Position.PositionString() : "Jail"),
                Color = Color.Gold
            };
            await SendBoard(Users, embed);
        }

        async Task SendToJail(IUser player)
        {
            pSt[player] = pSt[player] with { JailStatus = 0, Position = board.VisitingJailPosition };
            var embed = new EmbedBuilder()
            {
                Title = "Jail 🚔", //oncoming police car emoji
                Description = $"{player.Username} has been sent to jail.\n" +
                $"They can try rolling doubles up to 3 times, pay a fine of {board.JailFine.MoneyString()}, or use a Get out of Jail Free card.",
                Color = Color.Red
            }.Build();

            await this.Broadcast("", embed: embed);
        }

        async Task SendBoard(IEnumerable<IUser> users, EmbedBuilder? embed = null)
        {
            using var bmp = boardRenderer.Render(Players, pSt, board);
            using var memStr = new MemoryStream();
            bmp.Save(memStr, System.Drawing.Imaging.ImageFormat.Png);
            foreach (var u in users)
            {
                memStr.Position = 0;
                using var clone = new MemoryStream();
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

        async Task<int> MovePlayerRelative(IUser player, int amount)
        {
            var position = (pSt[player].Position + amount) % board.Spaces.Length;
            if (position < pSt[player].Position)
            {
                await GiveMoney(player, board.PassGoValue);
                var embed = new EmbedBuilder()
                {
                    Title = "Pass Go Bonus!",
                    Description = $"{player.Username} has gotten {board.PassGoValue.MoneyString()} for passing GO!"
                }.Build();
                await this.Broadcast("", embed: embed);
            }
            pSt[player] = pSt[player] with { Position = position};
            return position;
        }

        Color PlayerColor(IUser player) => pSt[player].Color.ToDiscordColor();

        async Task<bool> TryTransfer(IUser payer, int amount, IUser? reciever = null)
        {
            if (amount > pSt[payer].Money)
            {
                await this.BroadcastTo($"You do not have enough money to make this transaction. (You have: {pSt[payer].Money.MoneyString()}. Required amount: {amount.MoneyString()})", 
                    players: payer);
                return false;
            }

            pSt[payer] = pSt[payer] with { Money = pSt[payer].Money - amount };
            if (reciever is not null)
                await GiveMoney(reciever, amount, payer.Username);

            await this.
                BroadcastTo($"You have transferred {amount.MoneyString()} to {reciever?.Username ?? "the bank"}. Your balance is now {pSt[payer].Money.MoneyString()}.", players: payer);
            await this.Broadcast($"Transaction: {payer.Username} >> {amount.MoneyString()} >> {reciever?.Username ?? "Bank"}");
            return true;
        }

        async Task GiveMoney(IUser reciever, int amount, string? giver = null)
        {
            pSt[reciever] = pSt[reciever] with { Money = pSt[reciever].Money + amount };
            if (giver is not null)
                await this.BroadcastTo($"You have recieved {amount.MoneyString()} from {giver}. Your balance is now {pSt[reciever].Money.MoneyString()}.", players: reciever);
        }

        async Task<bool> TryBuyProperty(IUser player, int pos, int amount)
        {
            var space = board.Spaces[pos];
            if (space is not PropertySpace ps)
            {
                await this.BroadcastTo($"\"{space.Name}\" is not a property.", players: player);
                return false;
            }
            if (ps.Owner is not null)
            {
                await this.BroadcastTo($"\"{space.Name}\" is already owned by a player.", players: player);
                return false;
            }
            if (await TryTransfer(player, amount))
            {
                board.Spaces[pos] = ps with { Owner = player };
                var embed = new EmbedBuilder()
                {
                    Title = "Property Obtained",
                    Description = $"{player.Username} has obtained {ps.Name} ({pos.PositionString()}) for {amount.MoneyString()}!",
                    Color = board.GroupColorOrDefault(ps, Color.Green)
                }.Build();
                await this.Broadcast("", embed: embed);
                return true;
            }
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

            if (bid != -1)
                await this.Broadcast($"{currentAuctionPlayer.Username} has bid {bid.MoneyString()}.");
            else
                await this.Broadcast($"{currentAuctionPlayer.Username} has skipped.");

            if (!auctionState.Values.Contains(null) &&
                auctionState.Values.Count(x => x == -1) + 1>= auctionState.Count)
                // this checks if all, or 1 less than the total has skipped
            {
                await FinalizeAuction();
                return;
            }

            var nextPlayer = NextPlayer(currentAuctionPlayer);
            while (auctionState[nextPlayer] == -1)
                nextPlayer = NextPlayer(nextPlayer);


            if (nextPlayer.Id == currentAuctionPlayer.Id)
            {
                await FinalizeAuction();
            }
            else
            {
                await this.Broadcast($"{nextPlayer.Username} is the next to bid.");
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