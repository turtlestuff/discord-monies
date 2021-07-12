using Discord;
using Leisure;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordMoniesGame
{
    public sealed partial class DiscordMoniesGameInstance : GameInstance
    {
        ImmutableArray<Command> commands = default!;

        void RegisterCommands()
        {
            commands = new Command[]
            {
                new Command("bal", CanRun.Any, async (args, msg) =>
                {
                    IUser user;
                    if (args == "")
                    {
                        if (Spectators.Contains(msg.Author, DiscordComparers.UserComparer))
                        {
                            await this.BroadcastTo("As a spectator, you may only view other players' balances.");
                            return;
                        }
                        user = msg.Author;
                    }
                    else
                    {
                        user = Utils.MatchClosest(args, Players);
                    }

                    if (user is not null)
                    {
                        var embed = new EmbedBuilder()
                        {
                            Title = "Balance",
                            Description = $"{user.Username}'s balance is {playerStates[user].Money.MoneyString()}.",
                            Color = Color.Gold
                        }.Build();
                        await this.BroadcastTo("", embed: embed, players: msg.Author);
                        return;
                    }
                    await this.BroadcastTo($"Can't find player \"{args}\".");
                }),
                new Command("info", CanRun.Any, async (args, msg) =>
                {
                    try
                    {
                        var space = board.ParseBoardSpace(args);
                        var eb = new EmbedBuilder()
                        {
                            Title = "Space Info",
                            Color = Color.Gold,
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
                        }

                        await this.BroadcastTo("", embed: eb.Build(), players: msg.Author);
                    }
                    catch (ArgumentException ex)
                    {
                        await this.BroadcastTo(ex.Message, players: msg.Author);
                    }
                }),
                new Command("titledeed", CanRun.Any, async (args, msg) => 
                {
                    try
                    {
                        var s = board.ParseBoardSpaceInt(args);
                        var deed = board.TitleDeedFor(s);
                        var space = (PropertySpace) board.BoardSpaces[s]; //TitleDeedFor would have thrown if that isn't a property space :)

                        var eb = new EmbedBuilder()
                        {
                            Title = $"Title Deed for {space.Name}" + (space is RoadSpace rs ? $" ({board.GroupNames[rs.Group]}) " : ""),
                            Color = Color.Gold,
                            Fields = new()
                        };

                        if (space is TrainStationSpace)
                        {
                            var rentVal = deed.RentValues[0];
                            var text = $"If 1 R.R. is owned: {rentVal.MoneyString()}\n" +
                            $"If 2 R.R. are owned: {(rentVal * 2).MoneyString()}\n" +
                            $"If 3 R.R. are owned: {(rentVal * 3).MoneyString()}\n" +
                            $"If 4 R.R. are owned: {(rentVal * 4).MoneyString()}";
                            eb.Fields.Add(new() { IsInline = false, Name = "Rent Values" , Value = text});
                        }

                        if (space is UtilitySpace)
                        {
                            eb.Fields.Add(new() { IsInline = false, Name = "Rent Values" , Value =
                                "If one utility is owned, the rent is **4x** the value on the dice.\nIf both are owned, it is **10x** the value on the dice."});
                        }

                        if (space is RoadSpace)
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
                        }

                         eb.Fields.Add(new() { IsInline = true, Name = "Mortgage Value", Value = deed.MortgageValue.MoneyString() });

                        await this.BroadcastTo("", embed: eb.Build(), players: msg.Author);
                    }
                    catch (ArgumentException ex)
                    {
                        await this.BroadcastTo(ex.Message, players: msg.Author);
                    }
                }),
                new Command("board", CanRun.Any, async (args, msg) =>
                {
                    await SendBoard(new [] { msg.Author });
                }),
                new Command("roll", CanRun.CurrentPlayer, async (args, msg) =>
                {
                    continuousRolls++;
                    if (!canRoll)
                    {
                        await this.BroadcastTo("Cannot roll right now!", players: msg.Author);
                        return;
                    }
                    if (playerStates[msg.Author].JailStatus == -1) 
                    { 
                        var roll1 = Random.Shared.Next(1,7);
                        var roll2 = Random.Shared.Next(1,7);
                        var doubles = roll1 == roll2;
                        var speedLimit = continuousRolls >= 3;
                        var position = (playerStates[msg.Author].Position + roll1 + roll2) % board.BoardSpaces.Length;
                        var embed = new EmbedBuilder()
                        {
                            Title = "Roll �", //game die emoji
                            Description = $"{msg.Author.Username} has rolled `{roll1}` and `{roll2}`" +
                            (!speedLimit ? $"and has gone to space `{Board.PositionString(position)}` ({board.BoardSpaces[position].Name})." : "") +
                            (doubles && !speedLimit ? "\nSince they have rolled doubles, they may roll another time!" : "") +
                            (speedLimit ? ".\nHowever, as they have rolled doubles for the 3rd time, they have been sent to jail. No speeding!" : ""),
                            Color = Color.Gold
                        }.Build();

                        await this.Broadcast("", embed: embed);

                        if (speedLimit || board.BoardSpaces[position] is GoToJailSpace)
                        {
                            await SendToJail(currentPlr);
                            await AdvanceRound();
                            return;
                        }
                       
                        playerStates[msg.Author] = playerStates[msg.Author] with { Position = position };

                        if (board.BoardSpaces[position] is PropertySpace ps)
                        {
                            if (ps.Mortgaged || ps.Owner == currentPlr)
                            {
                                await AdvanceRound();
                                return; // no need to pay rent!
                            }

                            canRoll = false;

                            if (ps.Owner is null)
                            {
                                // Handle buy or auction
                            }

                            // Handle rent
                        }
                       
                         
                    }
                    // TODO: Handle jailed player
                    await AdvanceRound(); // TODO: Don't do this

                })
            }.ToImmutableArray();
        }

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
                cmdStr = msgContent[..index].Trim();
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
    }
}
