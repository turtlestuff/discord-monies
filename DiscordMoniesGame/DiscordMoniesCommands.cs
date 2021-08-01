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
                        if (!CurrentPlayers.Contains(msg.Author, DiscordComparers.UserComparer))
                        {
                            await msg.Author.SendMessageAsync("As a spectator, you may only view other players' information.");
                            return;
                        }
                        player = msg.Author;
                    }
                    else
                    {
                        var p = Utils.MatchClosest(args, CurrentPlayers, u => u.Username);
                        if (p is null)
                        {
                            await msg.Author.SendMessageAsync($"Player \"{args}\" not found.");
                            return;
                        }
                        player = p;
                    }


                    if (player is not null)
                    {
                        var playerState = plrStates[player];
                        var jailCard = JailCardOwnedBy(player) is not null ? "Yes" : "No";
                        var ownedProperty = board.OwnedBy<PropertySpace>(player).Select(x => board.LocName(Array.IndexOf(board.Spaces, x)));
                        var embed = new EmbedBuilder()
                        {
                            Title = player.Username,
                            Fields = new()
                            {
                                new() { IsInline = true, Name = "Balance", Value = playerState.Money.MoneyString() },
                                new() { IsInline = true, Name = "Position", Value = playerState.Position.LocString() },
                                new() { IsInline = true, Name = "Color", Value = Colors.NameOfColor(playerState.Color) },
                                new() { IsInline = true, Name = "Jail Card", Value = jailCard },
                                new() { IsInline = false, Name = "Properties Owned", Value = string.Join('\n', ownedProperty) }
                            },
                            Color = PlayerColor(player)
                        }.WithId(Id).Build();
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
                        if (!CurrentPlayers.Contains(msg.Author, DiscordComparers.UserComparer))
                        {
                            await msg.Author.SendMessageAsync("As a spectator, you may only view other players' information.");
                            return;
                        }
                        player = msg.Author;
                    }
                    else
                    {
                        var p = Utils.MatchClosest(args, CurrentPlayers, u => u.Username);
                        if (p is null)
                        {
                            await msg.Author.SendMessageAsync($"Player \"{args}\" not found.");
                            return;
                        }
                        player = p;
                    }

                    if (player is not null)
                    {
                        var playerState = plrStates[player];
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

                        if (space is ValueSpace vs) { eb.Fields.Add(new() { IsInline = true, Name = "Value", Value = vs.Value.MoneyString() }); } if (space is PropertySpace ps)
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

                        await this.BroadcastTo("", embed: eb.WithId(Id).Build(), players: msg.Author);
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
                        await this.BroadcastTo("", embed: board.CreateTitleDeedEmbed(s).WithId(Id).Build(), players: msg.Author);
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
                    }.WithId(Id).Build();
                    await this.BroadcastTo("", embed: embed, players: msg.Author);
                }),
                new("board", CanRun.Any, async (args, msg) =>
                {
                    await SendBoard(new [] { msg.Author });
                }),
                new("nudge", CanRun.Any, async (args, msg) =>
                {
                    var nudgePlayer = waiting switch
                    {
                        Waiting.ForAuctionToFinish => currentAuctionPlayer,
                        _ => currentPlr
                    };
                    if (nudgePlayer is null) { return; } await nudgePlayer.SendMessageAsync($"{msg.Author.Username} wished to remind you that it is your turn to play by giving you a gentle nudge. *Nudge!*");
                    await msg.Author.SendMessageAsync($"I've nudged {nudgePlayer.Username}.");
                }),
                new("help", CanRun.Any, async (args, msg) =>
                {
                    var anyCommands = string.Join('\n', commands.Where(c => c.CanRun == CanRun.Any).Select(c => $"`{c.Name}`"));
                    var plrCommands = string.Join('\n', commands.Where(c => c.CanRun == CanRun.Player).Select(c => $"`{c.Name}`"));
                    var curCommands = string.Join('\n', commands.Where(c => c.CanRun == CanRun.CurrentPlayer).Select(c => $"`{c.Name}`"));
                    var e = new EmbedBuilder()
                    {
                        Title = "Help",
                        Description = "[Click for more extensive help](https://turtlestuff.github.io/discord-monies)",
                        Fields = new()
                        {
                            new() { IsInline = true, Name = "Anyone", Value = anyCommands },
                            new() { IsInline = true, Name = "Player", Value = plrCommands },
                            new() { IsInline = true, Name = "Current Player", Value = curCommands }
                        }
                    }.WithId(Id).Build();
                    await msg.Author.SendMessageAsync(embed: e);
                }),

                new("roll", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (waiting != Waiting.ForNothing)
                    {
                        await msg.Author.SendMessageAsync("Cannot roll right now!");
                        return;
                    }
                    continuousRolls++;
                    if (plrStates[msg.Author].JailStatus == -1)
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
                            Description = $"**{msg.Author.Username}** has rolled {diceFaces[roll1]} {diceFaces[roll2]} " +
                            (!speedLimit ? $"and has gone to space **{board.LocName(position)}**" :
                            ".\nHowever, as they have rolled doubles for the 3rd time, they have been sent to jail. No speeding!"),
                            Color = PlayerColor(msg.Author)
                        }.WithId(Id).Build();

                        await this.Broadcast("", embed: embed);

                        if (speedLimit || board.Spaces[position] is GoToJailSpace)
                        {
                            await SendToJail(currentPlr);
                            await AdvanceRound();
                            return;
                        }

                        if (doubles) { rollAgain = true; } await HandlePlayerLand(position);
                        return;
                    }
                    else
                    {
                        var jailStatus = plrStates[msg.Author].JailStatus;

                        // player has another chance at rolling doubles
                        var roll1 = Random.Shared.Next(6) + 1;
                        var roll2 = Random.Shared.Next(6) + 1;
                        lastRoll = roll1 + roll2;
                        if (roll1 == roll2)
                        {
                            var position = await MovePlayerRelative(msg.Author, roll1 + roll2);
                            plrStates[msg.Author] = plrStates[msg.Author] with { JailStatus = -1 };
                            var embed = new EmbedBuilder()
                            {
                                Title = "Jail Roll",
                                Description = $"**{msg.Author.Username}** has rolled {diceFaces[roll1]} {diceFaces[roll2]}  and has been released from jail!\n" +
                                $"They move to {board.LocName(position)}",
                                Color = Color.Green,
                                Footer = new() { Text = "They do not get an extra turn for rolling doubles" }
                            }.WithId(Id).Build();
                            await this.Broadcast("", embed: embed);
                            await HandlePlayerLand(position);
                            return;
                        }
                        else
                        {
                            plrStates[msg.Author] = plrStates[msg.Author] with { JailStatus = jailStatus + 1 };
                            if (jailStatus + 1 == 3)
                            {
                                var embed = new EmbedBuilder()
                                {
                                    Title = "Jail Roll",
                                    Description = $"{msg.Author.Username} has rolled {diceFaces[roll1]} {diceFaces[roll2]}.",
                                    Color = Color.Red
                                }.WithId(Id).Build();
                                await this.Broadcast("", embed: embed);

                                waiting = Waiting.ForOtherJailDecision;
                                if (JailCardOwnedBy(msg.Author) is not null)
                                {
                                    await msg.Author.SendMessageAsync($"You have no remaining roll attempts. You must pay the fine of {board.JailFine.MoneyString()} using `bail` or use a " +
                                        "Get out of Jail Free card (`usejailcard`) in order to get out of jail.");
                                }
                                else
                                {
                                    await msg.Author.SendMessageAsync($"You have no remaining roll attempts. You must pay the fine of {board.JailFine.MoneyString()} using `bail`.");
                                }

                                jailRoll = roll1 + roll2;
                            }
                            else
                            {
                                var embed = new EmbedBuilder()
                                {
                                    Title = "Jail Roll",
                                    Description = $"{msg.Author.Username} has rolled {diceFaces[roll1]} {diceFaces[roll2]}. They have {3 - (jailStatus + 1)} more attempt(s) to roll doubles left.",
                                    Color = Color.Red
                                }.WithId(Id).Build();
                                await this.Broadcast("", embed: embed);
                                await AdvanceRound();
                            }
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

                    var aSt = plrStates[msg.Author];
                    var space = (PropertySpace) board.Spaces[aSt.Position];
                    if (await TryBuyProperty(msg.Author, aSt.Position, space.Value)) { await AdvanceRound(); } return;
                }),

                new("auctionthis", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (waiting != Waiting.ForAuctionOrBuyDecision)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    var aSt = plrStates[msg.Author];
                    var space = (PropertySpace) board.Spaces[aSt.Position];

                    auctionSpace = aSt.Position;
                    waiting = Waiting.ForAuctionToFinish;

                    currentAuctionPlayer = NextPlayer(msg.Author);
                    var sorted = OrderedPlayers(currentAuctionPlayer);
                    auctionState = new(sorted.Select(p => KeyValuePair.Create<IUser, int?>(p, null)), DiscordComparers.UserComparer);

                    var embed = new EmbedBuilder()
                    {
                        Title = "Auction 🧑‍⚖️", // judge emoji
                        Description = $"**{msg.Author.Username}** is auctioning off **{board.LocName(aSt.Position)}**, normally worth {space.Value.MoneyString()}.\n" +
                        $"The auction will start at {1.MoneyString()} minimum with **{currentAuctionPlayer.Username}**, and will continue in the order below. " +
                        "You may use `bid [amount]` to bid or `skip` to skip. " +
                        "It will end when all players but one have skipped, and the last player not to skip gets the property. " +
                        "If all players skip, the property is returned to the bank.",
                        Fields = new()
                        {
                            new() { IsInline = false, Name = "Order", Value = PrettyOrderedPlayers(currentAuctionPlayer, true, true, false)}
                        },
                        Color = board.GroupColorOrDefault(space)
                    }.WithId(Id).Build();
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

                    if (value > plrStates[msg.Author].Money)
                    {
                        await msg.Author.SendMessageAsync($"You do not have enough money to bid that much. Your balance is {plrStates[msg.Author].Money.MoneyString()}.");
                        return;
                    }

                    await NextBid(value);
                }),

                new("bail", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (plrStates[msg.Author].JailStatus == -1)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    await Transfer(board.JailFine, msg.Author);

                    var isJail3 = plrStates[msg.Author].JailStatus == 3;
                    plrStates[msg.Author] = plrStates[msg.Author] with { JailStatus = -1 };
                    var embed = new EmbedBuilder()
                    {
                        Title = "Jail Fine",
                        Description = $"**{msg.Author.Username}** has paid the fine of {board.JailFine.MoneyString()} and has been released from Jail",
                        Color = PlayerColor(msg.Author)
                    }.WithId(Id).Build();
                    await this.Broadcast("", embed: embed);

                    if (isJail3)
                    {
                        await HandlePlayerLand(await MovePlayerRelative(msg.Author, jailRoll));
                        return;
                    }

                    await msg.Author.SendMessageAsync("You may `roll` again.");
                    waiting = Waiting.ForNothing;

                }),
                new("usejailcard", CanRun.CurrentPlayer, async (args, msg) => {
                    if (plrStates[msg.Author].JailStatus == -1)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }

                    if (await TryTakeJailFreeCard(msg.Author)) {
                        var isJail3 = plrStates[msg.Author].JailStatus == 3;
                        plrStates[msg.Author] = plrStates[msg.Author] with { JailStatus = -1 };
                        var embed = new EmbedBuilder()
                        {
                            Title = "Jail Fine",
                            Description = $"**{msg.Author.Username}** has used a Get Out of Jail Free card and has been released from Jail",
                            Color = PlayerColor(msg.Author)
                        }.WithId(Id).Build();
                        await this.Broadcast("", embed: embed);

                        if (isJail3)
                        {
                            await HandlePlayerLand(await MovePlayerRelative(msg.Author, jailRoll));
                            return;
                        }

                        await msg.Author.SendMessageAsync("You may `roll` again.");
                        waiting = Waiting.ForNothing;
                    }
                }),

                new("mortgage", CanRun.Player, async (args, msg) =>
                {
                    try
                    {
                        foreach (var spaceArg in args.Split(" "))
                        {
                            var loc = board.ParseBoardSpaceInt(spaceArg);
                            var space = board.Spaces[loc];
                            if (space is not PropertySpace ps || ps.Owner?.Id != msg.Author.Id)
                            {
                                await msg.Author.SendMessageAsync($"{board.LocName(loc)} is not your property.");
                                continue;
                            }
                            if (ps.Mortgaged)
                            {
                                await msg.Author.SendMessageAsync($"{board.LocName(loc)} is already mortgaged. Use `demortgage` to de-mortgage.");
                                continue;
                            }

                            if (ps is RoadSpace rs)
                            {

                                if (board.IsEntireGroupOwned(rs.Group, msg.Author, out var spaces))
                                {
                                    //Ensure the entire group consists of undeveloped properties
                                    if (spaces.Any(space => space.Houses > 0))
                                    {
                                        await msg.Author.SendMessageAsync($"You cannot mortgage {board.LocName(loc)} right now as buildings currently exist on other properties in this set.");
                                        continue;
                                    }
                                }
                            }

                            var amt = board.TitleDeedFor(loc).MortgageValue;
                            board.Spaces[loc] = ps with { Mortgaged = true };
                            await combiningMessageManager.CombiningEmbedMessage(Users, msg.Author,
                                "Mortgage",
                                $"**{msg.Author.Username}** has mortgaged **{board.LocName(loc)}** for {amt.MoneyString()}.",
                                board.GroupColorOrDefault(ps, Color.Gold));

                            await Transfer(amt, null, msg.Author);
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
                        foreach (var spaceArg in args.Split(" "))
                        {
                            var loc = board.ParseBoardSpaceInt(spaceArg);
                            var space = board.Spaces[loc];
                            if (space is not PropertySpace ps || ps.Owner?.Id != msg.Author.Id)
                            {
                                await msg.Author.SendMessageAsync($"{board.LocName(loc)} is not your property.");
                                continue;
                            }
                            if (!ps.Mortgaged)
                            {
                                await msg.Author.SendMessageAsync($"{board.LocName(loc)} is not mortgaged.");
                                continue;
                            }
                            var mortgage = board.TitleDeedFor(loc).MortgageValue;
                            var amt = (int) (mortgage * 1.10); // mortgage value + 10%
                    
                            if (await TryTransfer(amt, msg.Author))
                            {
                                board.Spaces[loc] = ps with { Mortgaged = false };
                                await combiningMessageManager.CombiningEmbedMessage(Users, msg.Author,
                                    "Mortgage",
                                    $"**{msg.Author.Username}** has de-mortgaged **{board.LocName(loc)}** for {amt.MoneyString()}.",
                                    board.GroupColorOrDefault(ps, Color.Gold));
                            }
                        }
                    }
                    catch (ArgumentException e)
                    {
                        await msg.Author.SendMessageAsync(e.Message);
                    }
                }),
                new("build", CanRun.Player, async (args, msg) =>
                {
                    try
                    {
                        foreach (var spaceArgs in args.Split(" "))
                        {
                            var loc = board.ParseBoardSpaceInt(args);
                            if(!await TryDevelopSpace(msg.Author, loc, false))
                            {
                                return;
                            }
                        }
                    }
                    catch (ArgumentException e)
                    {
                        await msg.Author.SendMessageAsync(e.Message);
                    }
                }),
                new("demolish", CanRun.Player, async (args, msg) =>
                {
                    try
                    {
                        foreach (var spaceArgs in args.Split(" "))
                        {
                            var loc = board.ParseBoardSpaceInt(args);
                            if(!await TryDevelopSpace(msg.Author, loc, true))
                            {
                                return;
                            }
                        }
                    }
                    catch (ArgumentException e)
                    {
                        await msg.Author.SendMessageAsync(e.Message);
                    }
                }),

                new("trade", CanRun.Player, HandleTradeCommand),

                new("bankrupt", CanRun.Player, async (args, msg) => {
                    if (waiting != Waiting.ForArrearsPay)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }
                    if (plrStates[msg.Author].Money >= 0)
                    {
                        await msg.Author.SendMessageAsync("You are not in arrears!");
                        return;
                    }
                    if (args != "bankrupt")
                    {
                        var e = new EmbedBuilder()
                        {
                            Title = "Are you sure?",
                            Description = "Declaring bankruptcy will **exclude you from the game permanently**, and you will be put as a spectator. " +
                            "Only use this if you are sure you cannot raise enough funds to pay back your debt. To confirm your choice, type `bankrupt bankrupt`",
                            Color = Color.Red
                        }.WithId(Id).Build();
                        await msg.Author.SendMessageAsync(embed: e);
                        return;
                    }
                    await HandleBankruptcy(msg.Author);
                }),
                new("drop", CanRun.Any, async (args, msg) =>
                {
                    if (currentPlr.Id == msg.Author.Id)
                    {
                        await msg.Author.SendMessageAsync("You can't drop on your own turn. Please finish your round before dropping.");
                        return;
                    }

                    if (args != "drop")
                    {
                        var e = new EmbedBuilder()
                        {
                            Title = "Are you sure?",
                            Color = Color.Red
                        }.WithId(Id);
                        if (CurrentPlayers.Contains(msg.Author, DiscordComparers.UserComparer))
                        {
                            e.Description = "Running drop will effectively bankrupt you and **exclude you from the game permanently**, **without** being put as spectator. " +
                            "To confirm your choice, type `drop drop`";
                        }
                        else
                        {
                            e.Description = "Running drop will effectively bankrupt you and **exclude you from spectating permanently**. " +
                            "To confirm your choice, type `drop drop`";
                        }
                        await msg.Author.SendMessageAsync(embed: e.WithId(Id).Build());
                        return;
                    }
                    bankruptedPlayers.Remove(msg.Author);
                    if (CurrentPlayers.Contains(msg.Author, DiscordComparers.UserComparer)) { await HandleBankruptcy(msg.Author); } await DropPlayer(msg.Author);
                }),

                new("pay", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (waiting != Waiting.ForTakeCardOrPayDecision)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }
                    if (!payOrTakeCardVal.HasValue || !payOrTakeCardType.HasValue)
                    {
                        return;
                    }

                    if (await TryTransfer(payOrTakeCardVal.Value, msg.Author, null))
                    {
                        var e = new EmbedBuilder()
                        {
                            Title = "Pay or Take",
                            Description = $"{msg.Author.Username} has chosen to pay {payOrTakeCardVal.Value.MoneyString()}.",
                            Color = Color.Gold
                        }.WithId(Id).Build();
                        await this.Broadcast("", embed: e);
                        await AdvanceRound();
                    }
                }),
                new ("takecard", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    if (waiting != Waiting.ForTakeCardOrPayDecision)
                    {
                        await msg.Author.SendMessageAsync("You can't do this right now!");
                        return;
                    }
                    if (!payOrTakeCardVal.HasValue || !payOrTakeCardType.HasValue)
                    {
                        return;
                    }

                    await DrawCard(plrStates[currentPlr].Position, payOrTakeCardType.Value);
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
            {
                var closest = Utils.MatchClosest(cmdStr, commands, c => c.Name)?.Name;
                if (closest is not null && Math.Abs(cmdStr.Length - closest.Length) < 1)
                {
                    await msg.Author.SendMessageAsync($"Did you mean: `{closest}`?");
                }

                return false;
            }
            switch (commandObj.CanRun)
            {
                case CanRun.Any:
                    await commandObj.CmdFunc(args, msg);
                    return true;
                case CanRun.Player:
                    if (!CurrentPlayers.Contains(msg.Author, DiscordComparers.UserComparer))
                    {
                        return false;
                    }

                    await commandObj.CmdFunc(args, msg);
                    return true;
                case CanRun.CurrentPlayer:
                    if (currentPlr.Id != msg.Author.Id)
                    {
                        await this.BroadcastTo("Only the current player can run this!", players: msg.Author);
                        return false;
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
