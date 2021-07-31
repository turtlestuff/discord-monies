using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Immutable;

namespace DiscordMoniesGame
{
    record CombiningEmbedMessage(IUser Player, string Title, List<string> Body, List<IUserMessage> Messages, DateTime Expires);

    class CombiningMessageManager
    {
        readonly List<CombiningEmbedMessage> combiningMessages = new();
        readonly int id;

        ImmutableDictionary<IUser, IUserMessage>? transactionMessages;
        DateTime transactionMessageExpire = DateTime.MinValue;

        public CombiningMessageManager(int gameId)
        {
            id = gameId;
        }

        public async Task CombiningTransactionMessage(IEnumerable<IUser> users, IUser? payer, IUser? receiver, string? payerMessage, string? receiverMessage, string everyoneMessage)
        {
            if (transactionMessages is null || DateTime.Now >= transactionMessageExpire)
            {
                var tmsgs = new Dictionary<IUser, IUserMessage>(DiscordComparers.UserComparer);
                foreach (var user in users)
                {
                    if (user.Id == payer?.Id && payerMessage is not null)
                    {
                        tmsgs.Add(user, await user.SendMessageAsync(payerMessage));
                    }
                    else if (user.Id == receiver?.Id && receiverMessage is not null)
                    {
                        tmsgs.Add(user, await user.SendMessageAsync(receiverMessage));
                    }
                    else
                    {
                        tmsgs.Add(user, await user.SendMessageAsync(everyoneMessage));
                    }
                }
                transactionMessages = tmsgs.ToImmutableDictionary(DiscordComparers.UserComparer).WithComparers(DiscordComparers.UserComparer);
                transactionMessageExpire = DateTime.Now.AddSeconds(5);
            }
            else
            {
                foreach (var user in users)
                {
                    await transactionMessages[user].ModifyAsync(m =>
                    {
                        string msg;
                        if (user.Id == payer?.Id && payerMessage is not null)
                        {
                            msg = payerMessage;
                        }
                        else if (user.Id == receiver?.Id && receiverMessage is not null)
                        {
                            msg = receiverMessage;
                        }
                        else
                        {
                            msg = everyoneMessage;
                        }
                        
                        m.Content = transactionMessages[user].Content + "\n" + msg;  
                    });
                }
            }
        }

        public async Task CombiningEmbedMessage(IEnumerable<IUser> sendToUsers, IUser player, string title, string body, Color color)
        {
            if (combiningMessages.Any(m => m!.Title == title && m.Player.Id == player.Id))
            {
                var cmsg = combiningMessages.First(m => m!.Title == title && m.Player.Id == player.Id);
                var i = combiningMessages.IndexOf(cmsg);

                if (DateTime.Now >= cmsg.Expires)
                {
                    combiningMessages.RemoveAt(i);
                    combiningMessages.Add(await NewCombinedEmbedMessage(sendToUsers, player, title, body, color));
                    return;
                }

                cmsg.Body.Add(body);
                var embed = new EmbedBuilder()
                {
                    Title = title,
                    Description = string.Join('\n', cmsg.Body),
                    Color = color
                }.WithId(id).Build();
                foreach (var msg in cmsg.Messages)
                {
                    await msg.ModifyAsync(m => m.Embed = embed);
                }
            }
            else
            {
                combiningMessages.Add(await NewCombinedEmbedMessage(sendToUsers, player, title, body, color));
            }
        }

        async Task<CombiningEmbedMessage> NewCombinedEmbedMessage(IEnumerable<IUser> sendToUsers, IUser player, string title, string body, Color color)
        {
            var embed = new EmbedBuilder()
            {
                Title = title,
                Description = body,
                Color = color
            }.WithId(id).Build();

            var messages = new List<IUserMessage>();
            foreach (var plr in sendToUsers)
            {
                messages.Add(await plr.SendMessageAsync(embed: embed));
            }
            return new CombiningEmbedMessage(player, title, new() { body }, messages, DateTime.Now.AddSeconds(15));
        }
    }
}