using Discord;
using Leisure;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordMoniesGame
{
    public sealed partial class DiscordMoniesGameInstance : GameInstance
    {
        readonly TimeSpan closeTradeDelay = TimeSpan.FromMinutes(5);
        public sealed class TradeTable
        {
            public List<TradeItem> Give { get; init; } = new();
            public int GivingMoney { get; set; } = 0;
            public List<TradeItem> Take { get; init; } = new();

            public IUser? Sender { get; set; } = null;
            public IUser? Recipient { get; set; } = null;

            public enum TradeTableState
            {
                Creating,
                Offered,
                Closed
            }

            public TradeTableState State { get; set; } = TradeTableState.Creating;
        }

        readonly List<TradeTable> trades = new();

        public abstract record TradeItem();
        sealed record JailCardItem() : TradeItem();
        sealed record MoneyItem(int Amount) : TradeItem();
        sealed record PropertyItem(int Location, bool KeepMortgaged) : TradeItem();

        async Task HandleTradeCommand(string a, IUserMessage msg)
        {
            var i = a.IndexOf(' ');
            var command = i == -1 ? a : a[..i].Trim();
            var args = i == -1 ? "" : a[i..].Trim();
            var aSt = plrStates[msg.Author];
            switch (command.ToLowerInvariant())
            {
                case "new":
                    if (aSt.TradeTable is not null)
                    {
                        await msg.Author.SendMessageAsync("You have already laid out a trade. Use `trade close` to get rid of that one.");
                        return;
                    }
                    plrStates[msg.Author] = aSt with { TradeTable = new TradeTable() };
                    await msg.Author.SendMessageAsync("You have laid out a new trade.");
                    await SendTradeTable(plrStates[msg.Author].TradeTable!, msg.Author, false);
                    return;
                case "close":
                    if (aSt.TradeTable is null)
                        break;
                    plrStates[msg.Author] = aSt with { TradeTable = null };
                    await msg.Author.SendMessageAsync("You have chucked your old trade away.");
                    return;
                case "view":
                    if (aSt.TradeTable is null)
                        break;
                    await SendTradeTable(aSt.TradeTable, msg.Author, false);
                    return;
                case "give":
                    if (aSt.TradeTable is null)
                        break;
                    if (TryParseItem(args, out var item))
                    {
                        if (item is MoneyItem mi)
                        {
                            aSt.TradeTable.GivingMoney = mi.Amount;
                        }
                        else if (aSt.TradeTable.Give.Any(x => x == item))
                        {
                            // item already exists. remove
                            aSt.TradeTable.Give.Remove(item);
                        }
                        else
                        {
                            if (item is PropertyItem pi)
                            {
                                var index = aSt.TradeTable.Give.FindIndex(x => (x as PropertyItem)?.Location == pi.Location);
                                if (index != -1)
                                {
                                    // location is same, but different keepmortgage, switch it (will be added later)
                                    aSt.TradeTable.Give.RemoveAt(index);
                                    aSt.TradeTable.Give.Add(item);
                                }
                            }

                            aSt.TradeTable.Give.Add(item);
                        }
                        await SendTradeTable(aSt.TradeTable, msg.Author, false);
                        return;
                    }
                    await msg.Author.SendMessageAsync("Invalid argument format");
                    return;

                case "take":
                    if (aSt.TradeTable is null)
                        break;
                    if (TryParseItem(args, out var item1))
                    {
                        if (item1 is MoneyItem mi)
                        {
                            aSt.TradeTable.GivingMoney = -mi.Amount;
                        }
                        else if (aSt.TradeTable.Take.Any(x => x == item1))
                        {
                            // item already exists. remove
                            aSt.TradeTable.Take.Remove(item1);
                        }
                        else
                        {
                            if (item1 is PropertyItem pi)
                            {
                                var index = aSt.TradeTable.Take.FindIndex(x => (x as PropertyItem)?.Location == pi.Location);
                                if (index != -1)
                                {
                                    // location is same, but different keepmortgage, remove it (to be added later)
                                    aSt.TradeTable.Take.RemoveAt(index);
                                }
                            }

                            aSt.TradeTable.Take.Add(item1);
                        }
                        await SendTradeTable(aSt.TradeTable, msg.Author, false);
                        return;
                    }
                    await msg.Author.SendMessageAsync("Invalid argument format");
                    return;

                case "offer": //"request"
                    if (aSt.TradeTable is null)
                        break;
                    
                    var p = DetermineTradeTableParties(aSt.TradeTable.Take);
                    IUser recipient;

                    if (!p.Any())
                    {
                        await msg.Author.SendMessageAsync("No player matches you wish to recieve. Please make sure that one or more players own all that you want to trade.");
                        return;
                    }

                    if (args == "")
                    {
                        if (p.Count() != 1)
                        {
                            await msg.Author.SendMessageAsync($"{p.Select(p => p.Username).ToArray().CommaAndList()} are possible recipients for your trade. Please specify one with" +
                                "`trade offer [player]`");
                            return;
                        }
                        recipient = p.First();
                    }
                    else
                    {
                        var closestPlayer = Utils.MatchClosest(args, CurrentPlayers, x => x.Username);
                        if (!p.Contains(closestPlayer))
                        {
                            await msg.Author.SendMessageAsync($"Only {p.Select(p => p.Username).ToArray().CommaAndList()} are possible recipients for your trade. Please check that the " +
                                $"desired player ({closestPlayer.Username}) owns all the assets you wish to receive.");
                            return;
                        }
                        recipient = closestPlayer;
                    }

                    if (recipient.Id == msg.Author.Id)
                    {
                        await msg.Author.SendMessageAsync("You cannot trade with yourself!");
                        return;
                    }

                    aSt.TradeTable.Sender = msg.Author;
                    aSt.TradeTable.Recipient = recipient;

                    if (!EnsureItemsTradable(aSt.TradeTable))
                    {
                        await msg.Author.SendMessageAsync("This trade is invalid. Ensure that each user has the required assets and money to perform this trade.");
                        return;
                    }

                    aSt.TradeTable.State = TradeTable.TradeTableState.Offered;

                    if (!trades.Contains(aSt.TradeTable))
                        trades.Add(aSt.TradeTable);

                    await SendTradeTable(aSt.TradeTable, recipient, true);
                    await recipient.SendMessageAsync($"**{msg.Author.Username}** has offered you the trade above. You may accept this trade with " +
                        $"`trade accept {trades.IndexOf(aSt.TradeTable)}` or reject it with `trade reject {trades.IndexOf(aSt.TradeTable)}`");

                    plrStates[msg.Author] = aSt with { TradeTable = null };
                    await msg.Author.SendMessageAsync($"Trade offer has been sent to {recipient.Username} with index {trades.IndexOf(aSt.TradeTable)}. Your trade table has been closed.");
                    

                    return;
                case "viewoffer":
                    if (TryGetTradeTable(args, out var table))
                        await SendTradeTable(table, msg.Author, table.Recipient?.Id == msg.Author.Id);
                    else
                        await msg.Author.SendMessageAsync("That trade table is not available.");
                    return;

                case "accept":
                    if (TryGetTradeTable(args, out var table1) && table1.Recipient?.Id == msg.Author.Id)
                    {
                        if (!await TryConcludeTrade(table1))
                        {
                            trades.Remove(table1);
                            await msg.Author.SendMessageAsync("This trade is invalid. Ensure that each user has the required assets to perform this trade.");
                        }
                    }
                    else
                    {
                        await msg.Author.SendMessageAsync("That trade table is not available.");
                    }
                    return;
                case "reject":
                    if (TryGetTradeTable(args, out var table2) && table2.Recipient?.Id == msg.Author.Id)
                    {
                        await table2.Sender.SendMessageAsync($"**{msg.Author.Username}** has rejected your trade offer. You may recall this trade with `trade recall {args}`, to edit it.");
                        await msg.Author.SendMessageAsync($"You have rejected **{table2.Sender?.Username}**'s trade offer.");
                    }
                    else
                    {
                        await msg.Author.SendMessageAsync("That trade table is not available.");
                    }
                    return;
                case "copy":
                    if (TryGetTradeTable(args, out var table3) && table3.Recipient?.Id == msg.Author.Id)
                    {
                        if (aSt.TradeTable is not null)
                        {
                            await msg.Author.SendMessageAsync("You have already laid out a trade. Use `trade close` to get rid of that one.");
                            return;
                        }
                        plrStates[msg.Author] = aSt with
                        {
                            TradeTable = new()
                            {
                                Give = new(table3.Take),
                                Take = new(table3.Give),
                                GivingMoney = -table3.GivingMoney
                            }
                        };
                        trades.Remove(table3);
                        await table3.Sender.SendMessageAsync($"**{msg.Author.Username}** has copied your trade offer, and are currently editing it.");
                        await msg.Author.SendMessageAsync($"You have rejected and copied **{table3.Sender?.Username}**'s trade offer. " +
                            $"Their trade offer is now available in your trade table.");
                        await SendTradeTable(plrStates[msg.Author].TradeTable!, msg.Author, false);
                    }
                    else
                    {
                        await msg.Author.SendMessageAsync("That trade table is not available.");
                    }
                    return;
                case "recall":
                    if (TryGetTradeTable(args, out var table4))
                    {
                        if (aSt.TradeTable is not null)
                        {
                            await msg.Author.SendMessageAsync("You have already laid out a trade. Use `trade close` to get rid of that one.");
                            return;
                        }

                        if (table4.Sender?.Id == msg.Author.Id)
                        {
                            plrStates[msg.Author] = aSt with
                            {
                                TradeTable = new()
                                {
                                    Give = new(table4.Give),
                                    Take = new(table4.Take),
                                    GivingMoney = table4.GivingMoney
                                }
                            };
                        }
                        else if (table4.Recipient?.Id == msg.Author.Id)
                        {
                            plrStates[msg.Author] = aSt with
                            {
                                TradeTable = new()
                                {
                                    Give = new(table4.Take),
                                    Take = new(table4.Give),
                                    GivingMoney = -table4.GivingMoney
                                }
                            };
                        }
                        else
                        {
                            await msg.Author.SendMessageAsync("That trade table is not available.");
                            return;
                        }
                        await msg.Author.SendMessageAsync($"Trade {args} has been recalled.");
                        await SendTradeTable(plrStates[msg.Author].TradeTable!, msg.Author, false);
                        return;
                    }
                    else
                    {
                        await msg.Author.SendMessageAsync("That trade table is not available.");
                    }
                    return;
                default:
                    var ee = new EmbedBuilder()
                    {
                        Title = "Trade",
                        Description = "Invalid command for `trade`. [You can get help for this command here](https://turtlestuff.github.io/discord-monies/#Trading)"
                    }.WithId(Id).Build();
                    await msg.Author.SendMessageAsync(embed: ee);
                    return;
            }
            await msg.Author.SendMessageAsync("You don't have a trade laid out. Please use `trade new` to lay out a new one!");
        }

        async Task<bool> TryConcludeTrade(TradeTable table)
        {
            if (table.Recipient is null || table.Sender is null)
                return false;

            if (!EnsureItemsTradable(table))
                return false;

            var senderMortgage = MortgageAmount(table.Take);
            var recipientMortgage = MortgageAmount(table.Give);
            var senderGivingMoney = SenderAmount(table);
            var recipientGivingMoney = RecipientAmount(table);

            if (!await TryTransfer(senderMortgage, table.Sender, null) ||
                !await TryTransfer(recipientMortgage, table.Recipient, null) ||
                !await TryTransfer(senderGivingMoney, table.Sender, table.Recipient) ||
                !await TryTransfer(recipientGivingMoney, table.Recipient, table.Sender))
            {
                throw new TradeException("Money failed!");
            }

            var actions = new List<string>();

            if (senderMortgage > 0)
                actions.Add($"**{table.Sender.Username}**'s {senderMortgage.MoneyString()} ➡️ **the bank**");
            if (recipientMortgage > 0)
                actions.Add($"**{table.Recipient.Username}**'s {recipientMortgage.MoneyString()} ➡️ **the bank**");
            if (senderGivingMoney > 0)
                actions.Add($"**{table.Sender.Username}**'s {senderGivingMoney.MoneyString()} ➡️ **{table.Recipient.Username}**");
            if (recipientGivingMoney > 0)
                actions.Add($"**{table.Recipient.Username}**'s {recipientGivingMoney.MoneyString()} ➡️ **{table.Sender.Username}**");

            foreach (var giveItem in table.Give)
            {
                if (giveItem is JailCardItem)
                {
                    JailCardOwnedBy(table.Sender) = table.Recipient;
                    actions.Add($"**{table.Sender.Username}'s** Get out of Jail Free Card ➡️ **{table.Recipient.Username}**");
                    continue;
                }
                if (giveItem is PropertyItem pi)
                {
                    var space = (PropertySpace)board.Spaces[pi.Location];
                    TransferProperty(pi.Location, table.Recipient, pi.KeepMortgaged);
                    actions.Add($"{board.LocName(pi.Location)} ➡️ **{table.Recipient.Username}**" +
                        (space.Mortgaged ? (pi.KeepMortgaged ? " - kept mortgaged" : " - de-mortgaged") : ""));
                }
            }

            foreach (var takeItem in table.Take)
            {
                if (takeItem is JailCardItem)
                {
                    JailCardOwnedBy(table.Recipient) = table.Sender;
                    actions.Add($"**{table.Recipient.Username}'s** Get out of Jail Free Card ➡️ **{table.Sender.Username}**");
                    continue;
                }
                if (takeItem is PropertyItem pi)
                {
                    var space = (PropertySpace)board.Spaces[pi.Location];
                    TransferProperty(pi.Location, table.Sender, pi.KeepMortgaged);
                    actions.Add($"{board.LocName(pi.Location)} ➡️ **{table.Sender.Username}**" +
                        (space.Mortgaged ? (pi.KeepMortgaged ? " - kept mortgaged" : " - de-mortgaged") : ""));
                }
            }

            var embed = new EmbedBuilder()
            {
                Title = "Trade 🔀",
                Description = string.Join('\n', actions),
                Color = Color.Purple
            }.WithId(Id).Build();

            await this.Broadcast("", embed: embed);

            return true;
        }

        ref IUser? JailCardOwnedBy(IUser plr)
        {
            if (plr.Id == chanceJailFreeCardOwner?.Id)
                return ref chanceJailFreeCardOwner;
            else if (plr.Id == chestJailFreeCardOwner?.Id)
                return ref chestJailFreeCardOwner;
            throw new ArgumentException($"Player {plr.Username} does not own any jail card");
        }

        void TransferProperty(int loc, IUser toUser, bool keepMortgaged)
        {
            var space = (PropertySpace)board.Spaces[loc];
            board.Spaces[loc] = space with { Owner = toUser, Mortgaged = keepMortgaged & space.Mortgaged };
            // property should be mortgaged if it is already and if
            // keep mortgage is true
        }

        IEnumerable<IUser> DetermineTradeTableParties(IEnumerable<TradeItem> items)
        {
            if (!items.Any())
            {
                return CurrentPlayers;
            }

            var reducedByProperties = items.Select(item =>
            {
                if (item is PropertyItem pi)
                {
                    var space = (PropertySpace) board.Spaces[pi.Location];
                    if (space.Owner is null) throw new TradeException("No one owns this property");
                    return space.Owner;
                }
                return null;
            }).Distinct(DiscordComparers.UserComparer).Where(x => x is not null).Cast<IUser>();

            if (!reducedByProperties.Any())
                reducedByProperties = CurrentPlayers;   

            if (items.Any(x => x is JailCardItem))
            {
                var reducedByJailCard = CurrentPlayers
                    .Where(x => x.Id == chanceJailFreeCardOwner?.Id || x.Id == chestJailFreeCardOwner?.Id);
                return reducedByJailCard.Intersect(reducedByProperties, DiscordComparers.UserComparer);
            }

            return reducedByProperties;
        }

        bool EnsureItemsTradable(TradeTable table)
        {
            if (table.Take.Count != 0)
            {
                var p = DetermineTradeTableParties(table.Take);

                if (p.Count() != 1 || p.First().Id != table.Recipient?.Id)
                    return false;
            }

            if (table.Give.Count != 0)
            {
                var q = DetermineTradeTableParties(table.Give);

                if (q.Count() != 1 || q.First().Id != table.Sender?.Id)
                    return false;
            }

            return (plrStates[table.Sender!].Money - TotalSenderAmount(table) >= Math.Min(0, plrStates[table.Sender!].Money)) &&
                (plrStates[table.Recipient!].Money - TotalRecipientAmount(table) >= Math.Min(0, plrStates[table.Recipient!].Money));
        }

        int TotalSenderAmount(TradeTable table) =>
            SenderAmount(table) + MortgageAmount(table.Take);

        int TotalRecipientAmount(TradeTable table) =>
            RecipientAmount(table) + MortgageAmount(table.Give);

        static int SenderAmount(TradeTable table) => Math.Max(0, table.GivingMoney);
        static int RecipientAmount(TradeTable table) => Math.Max(0, -table.GivingMoney);

        int MortgageAmount(IEnumerable<TradeItem> items) =>
            items.Sum(i =>
            {
                if (i is not PropertyItem pi)
                    return 0; // not property item

                if (!((PropertySpace)board.Spaces[pi.Location]).Mortgaged)
                    return 0; // not mortgaged

                var deed = board.TitleDeedFor(pi.Location);
                // mortgaged. check if keeping or unmortgaging

                // keeping: pay 10% to the bank     // unmortgaging: pay 110% to the bank 
                return pi.KeepMortgaged ? (int)(deed.MortgageValue * 0.10) : (int)(deed.MortgageValue * 1.10);
            });


        async Task SendTradeTable(TradeTable table, IUser sendTo, bool reverse)
        {
            var embed = new EmbedBuilder()
            {
                Title = "Trade 🔀",
                Color = Color.Purple
            };
            var giveMortgageMoney = MortgageAmount(table.Take);
            var giveString = string.Join('\n', table.Give.Select(i => ItemName(i))) +
                (table.GivingMoney > 0 ? $"\n{SenderAmount(table).MoneyString()}" : "") +
                (giveMortgageMoney != 0 ? $"\n**To bank**: {giveMortgageMoney.MoneyString()}" : "");
            var giveField = new EmbedFieldBuilder()
            {
                Name = !reverse ? "You give:" : (table.Sender is null ? "They give:" : $"{table.Sender.Username} gives:"),
                IsInline = true,
                Value = giveString == "" ? "Empty" : giveString
            };

            var takeMortgageMoney = MortgageAmount(table.Give);
            var takeString = string.Join('\n', table.Take.Select(i => ItemName(i))) +
                (table.GivingMoney < 0 ? $"\n{RecipientAmount(table).MoneyString()}" : "") +
                (takeMortgageMoney != 0 ? $"\n**To bank**: {takeMortgageMoney.MoneyString()}" : "");

            var takeField = new EmbedFieldBuilder()
            {
                Name = !reverse ? (table.Recipient is null ? "They give:" : $"{table.Recipient.Username} gives:") : "You give:",
                IsInline = true,
                Value = takeString == "" ? "Empty" : takeString
            };
            embed.WithFields(reverse ? new[] { takeField, giveField } : new[] { giveField, takeField });
            await sendTo.SendMessageAsync(embed: embed.WithId(Id).Build());
        }

        string ItemName(TradeItem item) => item switch
        {
            JailCardItem => "Get out of Jail Free Card",
            PropertyItem pt => $"{board.LocName(pt.Location)}" +
            (((PropertySpace)board.Spaces[pt.Location]).Mortgaged ? (pt.KeepMortgaged ? ": keep mortgaged)" : ": de-mortgage") : ""),
            var x => x.ToString()
        };

        bool TryGetTradeTable(string args, [MaybeNullWhen(false)] out TradeTable table)
        {
            //maybe we should default to a sane trade table (the first one where they are the recipient?)
            table = default!;
            if (!int.TryParse(args, out var intResult))
                return false;

            if (intResult >= trades.Count)
                return false;

            table = trades[intResult];
            return true;
        }

        bool TryParseItem(string args, [MaybeNullWhen(false)] out TradeItem item)
        {
            if (int.TryParse(args, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var intResult))
            {
                item = new MoneyItem(intResult);
                return true;
            }
            if (args == "jailcard")
            {
                item = new JailCardItem();
                return true;
            }
            var split = args.Split(' ').Append("").ToArray();
            if (split.Length > 2)
            {
                if (board.TryParseBoardSpaceInt(split[0], out var loc) && board.Spaces[loc] is PropertySpace)
                {
                    var closest = Utils.MatchClosest(split[1], new[] { "demortgage", "keep" });
                    if (closest == "demortgage")
                    {
                        item = new PropertyItem(loc, false);
                        return true;
                    }
                    else
                    {
                        item = new PropertyItem(loc, true);
                        return true;
                    }
                }
            }
            else
            {
                if (board.TryParseBoardSpaceInt(split[0], out var loc) && board.Spaces[loc] is PropertySpace)
                {
                    item = new PropertyItem(loc, true);
                    return true;
                }
            }

            item = default!;
            return false;
        }
    }

    class TradeException : ApplicationException
    {
        public TradeException(string message) : base(message) { }
    }
}
