using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Numerics;

namespace DiscordMoniesGame
{
    record CombiningEmbedMessage(IUser Player, string Title, List<string> Body, List<IUserMessage> Messages, DateTime Expires);

    class CombiningMessageManager
    {
        readonly List<CombiningEmbedMessage> combiningMessages = new();
        readonly int id;

        public CombiningMessageManager(int gameId)
        {
            id = gameId;
        }

        public async Task CombinedEmbedMessage(IEnumerable<IUser> sendToUsers, IUser player, string title, string body, Color color)
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