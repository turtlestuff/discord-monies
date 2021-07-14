using Discord;
using Leisure;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordMoniesGame
{
    public sealed partial class DiscordMoniesGameInstance : GameInstance
    {
        #region Commands
        void RegisterCommands()
        {
            commands = new Command[]
            {
                new("player", CanRun.Any, async (args, msg) =>
                {
                    IUser player;
                    if (args == "")
                    {
                        if (Spectators.Contains(msg.Author, DiscordComparers.UserComparer))
                        {
                            await msg.Author.SendMessageAsync("As a spectator, you may only view other players' information.");
                            return;
                        }
                        player = msg.Author;
                    }
                    else
                    {
                        player = Utils.MatchClosest(args, Players);
                    }

                    if (player is not null)
                    {
                        var playerState = pSt[player];
                        var embed = new EmbedBuilder()
                        {
                            Title = player.Username,
                            Fields = new()
                            {
                                new() { IsInline = true, Name = "Balance", Value = playerState.Money.MoneyString() },
                                new() { IsInline = true, Name = "Position", Value = playerState.Position.PositionString() },
                                new() { IsInline = true, Name = "Color", Value = Colors.NameOfColor(playerState.Color) }
                            },
                            Color = PlayerColor(player)
                        }.Build();
                        await this.BroadcastTo("", embed: embed, players: msg.Author);
                        return;
                    }
                    await msg.Author.SendMessageAsync($"Can't find player \"{args}\".");
                }),
                new("bal", CanRun.Any, async (args, msg) =>
                {
                    IUser player;
                    if (args == "")
                    {
                        if (Spectators.Contains(msg.Author, DiscordComparers.UserComparer))
                        {
                            await msg.Author.SendMessageAsync("As a spectator, you may only view other players' information.");
                            return;
                        }
                        player = msg.Author;
                    }
                    else
                    {
                        player = Utils.MatchClosest(args, Players);
                    }

                    if (player is not null)
                    {
                        var playerState = pSt[player];
                        await msg.Author.SendMessageAsync($"{player.Username}'s balance: {playerState.Money.MoneyString()}");
                        return;
                    }
                    await msg.Author.SendMessageAsync($"Can't find player \"{args}\".");
                }),
                new("space", CanRun.Any, async (args, msg) =>
                {
                    try
                    {
                        var space = board.ParseBoardSpace(args);
                        var eb = new EmbedBuilder()
                        {
                            Title = "Space Info",
                            Fields = new List<EmbedFieldBuilder>
                            {
                                new() { IsInline = true, Name = "Type", Value = space.GetType().Name },
                                new() { IsInline = true, Name = "Name", Value = space.Name }
                            }
                        };

                        if (space is ValueSpace vs)
                            eb.Fields.Add(new() { IsInline = true, Name = "Value", Value = vs.Value.MoneyString() });

                        if (space is PropertySpace ps)
                        {
                            eb.Fields.Add(new() { IsInline = true, Name = "Owner", Value = ps.Owner?.Username ?? "None" });
                            eb.Fields.Add(new() { IsInline = true, Name = "Mortgaged?", Value=ps.Mortgaged ? "Yes" : "No" });
                        }

                        if (space is RoadSpace rs)
                        {
                            eb.Fields.Add(new() { IsInline = true, Name = "Color", Value = board.GroupNames[rs.Group] });
                            eb.Fields.Add(new() { IsInline = true, Name = "Buildings", Value = rs.Houses.BuildingsAsString() });
                            eb.Color = Colors.ColorOfName(board.GroupNames[rs.Group]).ToDiscordColor();
                        }

                        await this.BroadcastTo("", embed: eb.Build(), players: msg.Author);
                    }
                    catch (ArgumentException e)
                    {
                        await msg.Author.SendMessageAsync(e.Message);
                    }
                }),
                new("deed", CanRun.Any, async (args, msg) =>
                {
                    try
                    {
                        var s = board.ParseBoardSpaceInt(args);
                        var deed = board.TitleDeedFor(s);
                        var space = (PropertySpace) board.Spaces[s]; //TitleDeedFor would have thrown if that isn't a property space :)

                        var eb = new EmbedBuilder()
                        {
                             Title = $"Title Deed for {space.Name}" + (space is RoadSpace rs ? $" ({board.GroupNames[rs.Group]}) " : ""),
                             Description = $"Value: {space.Value.MoneyString()}",
                             Fields = new()
                        };

                        if (space is TrainStationSpace)
                        {
                            var rentVal = deed.RentValues[0];
                            var text = $"If 1 T.S. is owned: {rentVal.MoneyString()}\n" +
                            $"If 2 T.S. are owned: {(rentVal * 2).MoneyString()}\n" +
                            $"If 3 T.S. are owned: {(rentVal * 3).MoneyString()}\n" +
                            $"If 4 T.S. are owned: {(rentVal * 4).MoneyString()}";
                            eb.Fields.Add(new() { IsInline = false, Name = "Rent Values" , Value = text});
                        }

                        if (space is UtilitySpace)
                        {
                            eb.Fields.Add(new() { IsInline = false, Name = "Rent Values" , Value =
                                "If one utility is owned, the rent is **4x** the value on the dice.\nIf both are owned, it is **10x** the value on the dice."});
                        }

                        if (space is RoadSpace r)
                        {
                            var text = "";
                            for (var i = 0; i < deed.RentValues.Length; i++)
                            {
                                var value = deed.RentValues[i];
                                if (i == 0)
                                    text += $"**RENT**: {value.MoneyString()}\n*Rent doubled when entire group is owned*\n";
                                else
                                    text += $"With **{i.BuildingsAsString()}**: {value.MoneyString()}\n";
                            }
                            eb.Fields.Add(new() { IsInline = false, Name = "Rent Values", Value = text });
                            eb.Fields.Add(new() { IsInline = true, Name = "House Cost", Value = deed.HouseCost.MoneyString() });
                            eb.Fields.Add(new() { IsInline = true, Name = "Hotel Cost", Value = deed.HotelCost.MoneyString() });
                            eb.Color = Colors.ColorOfName(board.GroupNames[r.Group]).ToDiscordColor();
                        }

                        eb.Fields.Add(new() { IsInline = true, Name = "Mortgage Value", Value = deed.MortgageValue.MoneyString() });

                        await this.BroadcastTo("", embed: eb.Build(), players: msg.Author);
                    }
                    catch (ArgumentException e)
                    {
                        await msg.Author.SendMessageAsync(e.Message);
                    }
                }),
                new("status", CanRun.Any, async (args, msg) =>
                {
                    var embed = new EmbedBuilder()
                    {
                        Title = "Game Status",
                        Fields = new()
                        {
                            new() { IsInline = true, Name = "Round", Value = round},
                            new() { IsInline = true, Name = "Playing Order", Value = PrettyOrderedPlayers(currentPlr, false, true, true) },
                            new() { IsInline = true, Name = "Avaliable Houses", Value = board.AvailableHouses },
                            new() { IsInline = true, Name = "Avaliable Hotels", Value = board.AvailableHotels }
                        }
                    }.Build();
                    await this.BroadcastTo("", embed: embed, players: msg.Author);
                }),
                new("board", CanRun.Any, async (args, msg) =>
                {
                    await SendBoard(new [] { msg.Author });
                }),
                new("nudge", CanRun.Any, async (args, msg) =>
                {
                    await this.BroadcastTo($"{msg.Author.Username} wished to remind you that it is your turn to play by giving you a gentle nudge. *Nudge!*", players: currentPlr);
                    await msg.Author.SendMessageAsync($"I've nudged {currentPlr.Username}.");
                }),

                new("roll", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    continuousRolls++;
                    if (waiting != Waiting.ForNothing)
                    {
                        await msg.Author.SendMessageAsync("Cannot roll right now!");
                        return;
                    }
                    if (pSt[msg.Author].JailStatus == -1)
                    {
                        var roll1 = Random.Shared.Next(6) + 1;
                        var roll2 = Random.Shared.Next(6) + 1;
                        lastRoll = roll1 + roll2;
                        var doubles = roll1 == roll2;
                        var speedLimit = continuousRolls >= 3 && doubles;
                        var position = await MovePlayerRelative(msg.Author, roll1 + roll2);
                        var embed = new EmbedBuilder()
                        {
                            Title = "Roll 🎲", //game die emoji
                            Description = $"**{msg.Author.Username}** has rolled `{roll1}` and `{roll2}` " +
                            (!speedLimit ? $"and has gone to space `{position.PositionString()}` ({board.Spaces[position].Name})." : "") +
                            (speedLimit ? ".\nHowever, as they have rolled doubles for the 3rd time, they have been sent to jail. No speeding!" : ""),
                            Color = PlayerColor(msg.Author)
                        }.Build();

                        await this.Broadcast("", embed: embed);

                        if (speedLimit || board.Spaces[position] is GoToJailSpace)
                        {
                            await SendToJail(currentPlr);
                            await AdvanceRound();
                            return;
                        }

                        if (doubles)
                            doubleTurn = true;

                        await HandlePlayerLand(position);
                        return;
                    }
                    else
                    {
                        var jailStatus = pSt[msg.Author].JailStatus;
                        if (jailStatus < 3)
                        {
                            // player has another chance at rolling doubles
                            var roll1 = Random.Shared.Next(6) + 1;
                            var roll2 = Random.Shared.Next(6) + 1;
                            lastRoll = roll1 + roll2;
                            if (roll1 == roll2)
                            {
                                var position = await MovePlayerRelative(msg.Author, roll1 + roll2);
                                pSt[msg.Author] = pSt[msg.Author] with { JailStatus = -1 };
                                var embed = new EmbedBuilder()
                                {
                                    Title = "Jail Roll",
                                    Description = $"**{msg.Author.Username}** has rolled `{roll1}` and `{roll2}` and has been freed from jail!\n" +
                                    $"They move to `{position.PositionString()}` ({board.Spaces[position].Name})",
                                    Color = Color.Green,
                                    Footer = new(){ Text = "They do not get an extra turn for rolling doubles" }
                                }.Build();
                                await this.Broadcast("", embed: embed);
                                await HandlePlayerLand(position);
                                return;
                            }
                            else
                            {
                                pSt[msg.Author] = pSt[msg.Author] with { JailStatus = jailStatus + 1 };
                                var embed = new EmbedBuilder()
                                {
                                    Title = "Jail Roll",
                                    Description = $"{msg.Author.Username} has rolled `{roll1}` and `{roll2}`. They have {3 - (jailStatus + 1)} more attempt(s) to roll doubles left.",
                                    Color = Color.Red
                                }.Build();
                                await this.Broadcast("", embed: embed);
                                await AdvanceRound();
                                return;
                            }
                        }
                        else
                        {
                            waiting = Waiting.ForOtherJailDecision;
                            await msg.Author.SendMessageAsync($"You have no remaining roll attempts. You must pay the fine of {board.JailFine.MoneyString()} or use a " + "Get out of Jail Free card in order to get out of jail.");
                            return;
                        }
                    }
                }),

                new("buythis", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (waiting != Waiting.ForAuctionOrBuyDecision)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    var aSt = pSt[msg.Author];
                    var space = (PropertySpace) board.Spaces[aSt.Position];
                    if (await TryBuyProperty(msg.Author, aSt.Position, space.Value))
                        await AdvanceRound();

                    return;
                }),

                new("auctionthis", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (waiting != Waiting.ForAuctionOrBuyDecision)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    var aSt = pSt[msg.Author];
                    var space = (PropertySpace) board.Spaces[aSt.Position];

                    auctionSpace = aSt.Position;
                    waiting = Waiting.ForAuctionToFinish;

                    currentAuctionPlayer = NextPlayer(msg.Author);
                    var sorted = OrderedPlayers(currentAuctionPlayer);
                    auctionState = new(sorted.Select(p => KeyValuePair.Create<IUser, int?>(p, null)), DiscordComparers.UserComparer);

                    var embed = new EmbedBuilder()
                    {
                        Title = "Auction 🧑‍⚖️", // judge emoji
                        Description = $"**{msg.Author.Username}** is auctioning off **{space.Name}** ({aSt.Position.PositionString()}), normally worth {space.Value.MoneyString()}.\n" +
                        $"The auction will start at {1.MoneyString()} minimum with **{currentAuctionPlayer.Username}**, and will continue in the order below. " +
                        "You may use `bid [amount]` to bid or `skip` to skip. " +
                        "It will end when all players but one have skipped, and the last player not to skip gets the property. " +
                        "If all players skip, the property is returned to the bank.",
                        Fields = new()
                        {
                            new() { IsInline = false, Name = "Order", Value = PrettyOrderedPlayers(currentAuctionPlayer, true, true, false)}
                        },
                        Color = board.GroupColorOrDefault(space)
                    }.Build();
                    await this.Broadcast("", embed: embed);
                }),
                new("skip", CanRun.Player, async (args, msg) =>
                {
                    if (waiting != Waiting.ForAuctionToFinish || currentAuctionPlayer?.Id != msg.Author.Id)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    if (auctionState is null)
                    {
                        await FinalizeAuction();
                        return;
                    }

                    await NextBid(-1);
                }),
                new("bid", CanRun.Player, async (args, msg) =>
                {
                    if (waiting != Waiting.ForAuctionToFinish || currentAuctionPlayer?.Id != msg.Author.Id)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    if (auctionState is null)
                    {
                        await FinalizeAuction();
                        return;
                    }

                    var value = int.Parse(args, NumberStyles.AllowThousands);
                    var minBid = auctionState.Max(x => x.Value ?? 0);

                    if (value <= minBid)
                    {
                        await msg.Author.SendMessageAsync($"That bid is too low. The minimum bid is {(minBid + 1).MoneyString()}.");
                        return;
                    }

                    if (value > pSt[msg.Author].Money)
                    {
                        await msg.Author.SendMessageAsync($"You do not have enough money to bid that much. Your balance is {pSt[msg.Author].Money.MoneyString()}.");
                        return;
                    }

                    await NextBid(value);
                }),

                new("paytax", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (waiting != Waiting.ForTaxPay)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    var aSt = pSt[msg.Author];
                    var space = (TaxSpace) board.Spaces[aSt.Position];
                    if (await TryTransfer(space.Value, msg.Author))
                        await AdvanceRound();

                    return;
                }),
                new("payrent", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (waiting != Waiting.ForRentPay)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    var aSt = pSt[msg.Author];
                    var space = (PropertySpace) board.Spaces[aSt.Position];

                    if (space.Owner is null || space.Mortgaged || space.Owner.Id == currentPlr.Id) //vroom vroom condition
                    {
                        await AdvanceRound();
                        return;
                    }

                    var rent = board.CalculateRentFor(aSt.Position);
                    if (space is UtilitySpace)
                        rent *= lastRoll;

                    if (await TryTransfer(rent, msg.Author, space.Owner))
                    {
                        await this.BroadcastTo($"**{msg.Author}** has paid you rent.");
                        await AdvanceRound();
                    }
                    return;
                }),

                new("bail", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (pSt[msg.Author].JailStatus == -1)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    if (await TryTransfer(board.JailFine, msg.Author))
                    {
                        pSt[msg.Author] = pSt[msg.Author] with { JailStatus = -1 };
                        var embed = new EmbedBuilder()
                        {
                            Title = "Jail Fine",
                            Description = $"**{msg.Author.Username}** has paid the fine of {board.JailFine.MoneyString()} and has gotten out of Jail",
                            Color = PlayerColor(msg.Author)
                        }.Build();
                        await this.Broadcast("", embed: embed);
                        await AdvanceRound();
                    }
                }),
                new("usejailcard", CanRun.CurrentPlayer, async (args, msg) => { }),

                new("mortgage", CanRun.Player, async (args, msg) =>
                {
                    try
                    {
                        var loc = board.ParseBoardSpaceInt(args);
                        var space = board.Spaces[loc];
                        if (space is not PropertySpace ps || ps.Owner?.Id != msg.Author.Id)
                        {
                            await msg.Author.SendMessageAsync($"{space.Name} is not your property.");
                            return;
                        }
                        if (ps.Mortgaged)
                        {
                            await msg.Author.SendMessageAsync($"{space.Name} is already mortgaged. Use `demortgage` to de-mortgage.");
                            return;
                        }
                        if (ps is RoadSpace rs && rs.Houses != 0)
                        {
                            await msg.Author.SendMessageAsync("You may only mortgage a property which has no buildings.");
                            return;
                        }

                        var amt = board.TitleDeedFor(loc).MortgageValue;
                        if(await TryTransfer(amt, null, msg.Author))
                        {
                            board.Spaces[loc] = ps with { Mortgaged = true };
                            await this.Broadcast($"**{msg.Author.Username}** has mortgaged **{space.Name}** ({loc.PositionString()}) for {amt.MoneyString()}.");
                        }
                    }
                    catch (ArgumentException e)
                    {
                        await msg.Author.SendMessageAsync(e.Message);
                    }
                }),
                new("demortgage", CanRun.Player, async (args, msg) => 
                {
                    try
                    {
                    var loc = board.ParseBoardSpaceInt(args);
                    var space = board.Spaces[loc];
                    if (space is not PropertySpace ps || ps.Owner?.Id != msg.Author.Id)
                    {
                        await msg.Author.SendMessageAsync($"{space.Name} ({loc.PositionString()}) is not your property.");
                        return;
                    }
                    if (!ps.Mortgaged)
                    {
                        await msg.Author.SendMessageAsync($"{space.Name} ({loc.PositionString()}) is not mortgaged.");
                        return;
                    }
                    var mortgage = board.TitleDeedFor(loc).MortgageValue;
                    var amt = (int) (mortgage * 1.10); // mortgage value + 10%
                    
                    if (await TryTransfer(amt, msg.Author))
                    {
                        board.Spaces[loc] = ps with { Mortgaged = false };
                        await this.Broadcast($"**{msg.Author.Username}** has unmortgaged **{space.Name}** ({loc.PositionString()}).");
                    }
                    }
                    catch (ArgumentException e)
                    {
                        await msg.Author.SendMessageAsync(e.Message);
                    }
                })

            }.ToImmutableArray();
        }
        #endregion

        #region Command Handler
        ImmutableArray<Command> commands = default!;

        async Task<bool> TryHandleCommand(string msgContent, IUserMessage msg)
        {  
            var index = msgContent.IndexOf(' ');
            string cmdStr, args;
            if (index == -1)
            {
                cmdStr = msgContent;
                args = "";
            }
            else
            {   
                cmdStr = msgContent[..index].Trim().ToLowerInvariant();
                args = msgContent[index..].Trim();
            }

            var commandObj = commands.FirstOrDefault(c => c.Name == cmdStr);

            if (commandObj is null)
                return false;

            switch (commandObj.CanRun)
            {
                case CanRun.Any:
                    await commandObj.CmdFunc(args, msg);
                    return true;
                case CanRun.Player:
                    if (!Players.Contains(msg.Author, DiscordComparers.UserComparer))
                        return false;
                    await commandObj.CmdFunc(args, msg);
                    return true;
                case CanRun.CurrentPlayer:
                    if (currentPlr.Id != msg.Author.Id)
                    {
                        await this.BroadcastTo("Only the current player can run this!", players: msg.Author);
                        return true; //don't chat that!
                    }
                    await commandObj.CmdFunc(args, msg);
                    return true;
            }
            return false;

        }

        record Command(string Name, CanRun CanRun, Func<string, IUserMessage, Task> CmdFunc);

        enum CanRun
        {
            Any,
            Player,
            CurrentPlayer
        }
        #endregion
    }
}
