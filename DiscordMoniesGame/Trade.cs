using Discord;
using Leisure;
using System;
using System.Collections.Concurrent;
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
                case "start":
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
                        var p = aSt.TradeTable.Take.Select(DetermineTradeTableParties).Distinct().Where(x => x is not null).Cast<IUser>();

                        if (p.Count() != 1)
                            throw new ArgumentException("Ambiguous Trade Offer");

                        aSt.TradeTable.Sender = msg.Author;
                        aSt.TradeTable.Recipient = p.First();

                        if (!EnsureItemsTradable(aSt.TradeTable))
                        {
                            await msg.Author.SendMessageAsync("This trade is invalid. Ensure that each user has the required assets to perform this trade.");
                            return;
                        }

                        aSt.TradeTable.State = TradeTable.TradeTableState.Offered;
                        trades.Add(aSt.TradeTable);
                        await SendTradeTable(aSt.TradeTable, p.First(), true);
                    }
                    catch (ArgumentException)
                    {
                        await msg.Author.SendMessageAsync("The trade table is ambiguous or invalid. Ensure that the trade table consists of " +
                            "properties where you are trading between yourself and exactly one other player.");
                    }

                    return;
                case "viewoffer":
                    if (TryGetTradeTable(args, out var table))
                        await SendTradeTable(table, msg.Author, table.Recipient?.Id == msg.Author.Id);
                    else  //i am sorry in advance for my formatting
                        await msg.Author.SendMessageAsync("That trade table is not available.");
                    
                    return;
                case "accept":
                    return;
                case "copy":
                    return;
                case "reject":
                    return;
                default:
                    await msg.Author.SendMessageAsync("Invalid command for trade");
                    return;
            }
            await msg.Author.SendMessageAsync("You don't have a trade laid out. Please use `trade start` to lay out a new one!");
        }

        IUser? DetermineTradeTableParties(TradeItem item)
        {
            if (item is PropertyItem pi)
            {
                var space = (PropertySpace)board.Spaces[pi.Location];
                if (space.Owner is null) throw new ArgumentException("No one owns this property");
                return space.Owner;
            }
            else if (item is JailCardItem)
            {
                //for now just return the chance card
                return chanceJailFreeCardOwner ?? chestJailFreeCardOwner ??
                throw new ArgumentException("No one owns a GOOJFC"); //abort the trade right now
            }
            return null;
        }

        bool EnsureItemsTradable(TradeTable table)
        {
            try
            {
                var p = table.Take.Select(DetermineTradeTableParties).Distinct().Where(x => x is not null).Cast<IUser>();

                if (p.Count() > 1)
                    throw new ArgumentException("Ambiguous Trade Offer");

                if (p.Count() == 1 && p.First().Id != table.Recipient?.Id) return false;

                var q = table.Give.Select(DetermineTradeTableParties).Distinct().Where(x => x is not null).Cast<IUser>();

                if (q.Count() > 1)
                    throw new ArgumentException("Ambiguous Trade Offer");

                if (q.Count() == 1 && q.First().Id != table.Sender?.Id)
                    return false;

                if (table.GivingMoney > 0)
                {
                    return plrStates[table.Sender!].Money > table.GivingMoney;
                }
                else if (table.GivingMoney < 0)
                {
                    //help we need to make sure the people have enough money
                    return plrStates[table.Recipient!].Money > -table.GivingMoney;
                }

                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        async Task SendTradeTable(TradeTable table, IUser sendTo, bool reverse)
        {
            var embed = new EmbedBuilder()
            {
                Title = "Trade 🔀",
                Color = Color.Purple
            };
            var giveString = string.Join('\n', table.Give.Select(i => ItemName(i))) +
                (table.GivingMoney > 0 ? $"\n{table.GivingMoney.MoneyString()}" : "");
            var giveField = new EmbedFieldBuilder()
            {
                Name = !reverse ? "You give:" : "You take:",
                IsInline = true,
                Value = giveString == "" ? "Empty" : giveString
            };
            var takeString = string.Join('\n', table.Take.Select(i => ItemName(i))) +
                (table.GivingMoney < 0 ? $"\n{(-table.GivingMoney).MoneyString()}" : "");
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
        }// AWFEJNAWEFJAWEFLAWEFANWLJEF

        bool TryParseItem(string args, out TradeItem item)
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
            var split = args.Split(' ');
            if (split.Length == 2)
            {
                if (board.TryParseBoardSpaceInt(split[0], out var loc) && board.Spaces[loc] is PropertySpace)
                {
                    if (split[1] == "keep")
                    {
                        item = new PropertyItem(loc, true);
                        return true;
                    }
                    if (split[1] == "demortgage")
                    {
                        item = new PropertyItem(loc, false);
                        return true;
                    }
                }
            }
            
            item = default!;
            return false;
        }
    }
}
