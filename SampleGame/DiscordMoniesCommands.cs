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
                        user = msg.Author;
                    else
                        user = Utils.MatchClosest(args, Players);

                    if (user is not null)
                    {
                        var embed = new EmbedBuilder()
                        {
                            Title = "Balance",
                            Description = $"{user.Username}'s balance is `Ð{playerStates[user].Money:N0}`.",
                            Color = Color.Gold
                        }.Build();
                        await this.BroadcastTo("", embed: embed, players: msg.Author);
                        return;
                    }
                    await this.BroadcastTo($"Can't find player \"{args}\".");
                }),

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
                    if (!currentUser.Equals(msg.Author))
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
