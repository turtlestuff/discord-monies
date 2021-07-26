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
            public List<TradeItem> Give { get; } = new();
            public int GivingMoney { get; set; } = 0;
            public List<TradeItem> Take { get; } = new();

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
                    trades.Remove(aSt.TradeTable);
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
                            aSt.TradeTable.GivingMoney += mi.Amount;
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
                                    // location is same, but different keepmortgage, switch it
                                    aSt.TradeTable.Give.RemoveAt(index);
                                    aSt.TradeTable.Give.Add(item);
                                }
                            }
                            // new stuff! add it
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
                            aSt.TradeTable.GivingMoney -= mi.Amount;
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
                                    // location is same, but different keepmortgage, switch it
                                    aSt.TradeTable.Take.RemoveAt(index);
                                    aSt.TradeTable.Take.Add(item1);
                                }
                            }
                            // new stuff! add it
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

                    try
                    {
                        var p = DetermineTradeTableParties(aSt.TradeTable.Take);

                        if (p is null)
                            throw new TradeException("Ambiguous Trade Offer");

                        aSt.TradeTable.Sender = msg.Author;
                        aSt.TradeTable.Recipient = p;

                        if (!EnsureItemsTradable(aSt.TradeTable))
                        {
                            await msg.Author.SendMessageAsync("This trade is invalid. Ensure that each user has the required assets to perform this trade.");
                            return;
                        }

                        aSt.TradeTable.State = TradeTable.TradeTableState.Offered;
                        trades.Add(aSt.TradeTable);
                        await SendTradeTable(aSt.TradeTable, p, true);
                        await p.SendMessageAsync($"**{msg.Author.Username}** has offered you the trade above. You may accept this trade with " +
                            $"`trade accept {trades.IndexOf(aSt.TradeTable)}` or reject it with `trade reject {trades.IndexOf(aSt.TradeTable)}`");
                    }
                    catch (TradeException)
                    {
                        await msg.Author.SendMessageAsync("The trade table is ambiguous or invalid. Ensure that the trade table consists of " +
                            "properties where you are trading between yourself and exactly one other player.");
                    }

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
                        trades.Remove(table2);
                        await table2.Sender.SendMessageAsync($"**{msg.Author.Username}** has rejected your trade offer.");
                        await msg.Author.SendMessageAsync($"You have rejected **{table2.Sender?.Username}**'s trade offer.");
                    }
                    else
                    {
                        await msg.Author.SendMessageAsync("That trade table is not available.");
                    }
                    return;
                default:
                    await msg.Author.SendMessageAsync("Invalid command for trade");
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

            foreach (var giveItem in table.Give)
            {
                if (giveItem is JailCardItem)
                {
                    JailCardThatPlayerOwns(table.Sender) = table.Recipient;
                    actions.Add($"**{table.Sender.Username}'s** Get out of Jail Free Card ➡️ **{table.Recipient.Username}**");
                    continue;
                }
                if (giveItem is PropertyItem pi)
                {
                    var space = (PropertySpace)board.Spaces[pi.Location];
                    TransferProperty(pi.Location, table.Recipient, pi.KeepMortgaged);
                    actions.Add($"**{space.Name}** ({pi.Location.LocString()}) ➡️ **{table.Recipient.Username}**" +
                        (space.Mortgaged ? (pi.KeepMortgaged ? "(Kept Mortgaged)" : "(De-mortgaged)") : ""));
                }
            }

            foreach (var takeItem in table.Take)
            {
                if (takeItem is JailCardItem)
                {
                    JailCardThatPlayerOwns(table.Recipient) = table.Sender;
                    actions.Add($"**{table.Recipient.Username}'s** Get out of Jail Free Card ➡️ **{table.Sender.Username}**");
                    continue;
                }
                if (takeItem is PropertyItem pi)
                {
                    var space = (PropertySpace)board.Spaces[pi.Location];
                    TransferProperty(pi.Location, table.Sender, pi.KeepMortgaged);
                    actions.Add($"**{space.Name}** ({pi.Location.LocString()}) ➡️ **{table.Sender.Username}**" +
                        (space.Mortgaged ? (pi.KeepMortgaged ? "(Kept Mortgaged)" : "(De-mortgaged)") : ""));
                }
            }

            var embed = new EmbedBuilder()
            {
                Title = "Trade 🔀",
                Description = string.Join('\n', actions),
                Color = Color.Purple
            }.Build();

            await this.Broadcast("", embed: embed);

            return true;
        }

        ref IUser? JailCardThatPlayerOwns(IUser plr)
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

        IUser? DetermineTradeTableParties(IEnumerable<TradeItem> items)
        {
            var reduced = items.Select(item =>
            {
                if (item is PropertyItem pi)
                {
                    var space = (PropertySpace)board.Spaces[pi.Location];
                    if (space.Owner is null) throw new TradeException("No one owns this property");
                    return space.Owner;
                }
                return null;
            }).Distinct().Where(x => x is not null).Cast<IUser>();

            if (reduced.Count() > 1)
                return null;

            var plr = reduced.FirstOrDefault();

            if (items.Any(x => x is JailCardItem))
            {
                if (plr is null)
                {
                    //Determine the player from the GOOJFC
                    return (chanceJailFreeCardOwner, chestJailFreeCardOwner) switch
                    {
                        //No one owns a GOOJFC
                        (null, null) => null, 

                        //One GOOJFC is owned by any player
                        (null, not null) => chestJailFreeCardOwner,
                        (not null, null) => chanceJailFreeCardOwner,

                        //The same person owns both GOOJFCs
                        (not null, not null) when chanceJailFreeCardOwner.Id == chestJailFreeCardOwner.Id => chanceJailFreeCardOwner,

                        //A different person owns each GOOJFC
                        _ => null 
                    };
                }

                if (plr.Id == chanceJailFreeCardOwner?.Id || plr.Id == chestJailFreeCardOwner?.Id)
                    return plr;
                else
                    return null;
            }

            return plr;
        }

        bool EnsureItemsTradable(TradeTable table)
        {
            // TODO: This is a really weird way to do things! 
            try
            {
                if (table.Take.Count != 0)
                {
                    var p = DetermineTradeTableParties(table.Take);

                    if (p is null)
                        throw new TradeException("Ambiguous Trade Offer");

                    if (p.Id != table.Recipient?.Id)
                        return false;
                }

                if (table.Give.Count != 0)
                {
                    var q = DetermineTradeTableParties(table.Give);

                    if (q is null)
                        throw new TradeException("Ambiguous Trade Offer");

                    if (q.Id != table.Sender?.Id)
                        return false;
                }

                return (plrStates[table.Sender!].Money >= TotalSenderAmount(table)) &&
                    (plrStates[table.Recipient!].Money >= TotalRecipientAmount(table));
            }
            catch (TradeException)
            {
                return false;
            }
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

            var giveMortgageMoney = MortgageAmount(table.Give);
            var giveString = string.Join('\n', table.Give.Select(i => ItemName(i))) +
                (table.GivingMoney > 0 ? $"\n{SenderAmount(table).MoneyString()}" : "") +
                (giveMortgageMoney != 0 ? $"\n**To bank**: {giveMortgageMoney.MoneyString()}" : "");
            var giveField = new EmbedFieldBuilder()
            {
                Name = !reverse ? "You give:" : "You take:",
                IsInline = true,
                Value = giveString == "" ? "Empty" : giveString
            };

            var takeMortgageMoney = MortgageAmount(table.Take);
            var takeString = string.Join('\n', table.Take.Select(i => ItemName(i))) +
                (table.GivingMoney < 0 ? $"\n{RecipientAmount(table).MoneyString()}" : "") +
                (takeMortgageMoney != 0 ? $"\n**To bank**: {takeMortgageMoney.MoneyString()}" : "");

            var takeField = new EmbedFieldBuilder()
            {
                Name = !reverse ? "You take:" : "You give:",
                IsInline = true,
                Value = takeString == "" ? "Empty" : takeString
            };
            embed.WithFields(reverse ? new[] { takeField, giveField } : new[] { giveField, takeField });
            await sendTo.SendMessageAsync(embed: embed.Build());
        }

        string ItemName(TradeItem item) => item switch
        {
            JailCardItem => "Get out of Jail Free Card",
            PropertyItem pt => $"{pt.Location.LocString()}: {board.Spaces[pt.Location].Name}" +
            (((PropertySpace)board.Spaces[pt.Location]).Mortgaged ? (pt.KeepMortgaged ? "(Keep)" : "(Demortgage)") : ""),
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
                    if (split[1] == "demortgage")
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

            item = default!;
            return false;
        }
    }

    class TradeException : ApplicationException
    {
        public TradeException(string message) : base(message) { }
    }
}
