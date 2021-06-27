using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace Leisure
{
    static partial class Program
    {
        static Task ParseDmMessage(IUserMessage msg)
        {
            var pos = 0;
            
            // If the message starts with #<number>, try and parse out a game ID
            if (msg.Content[0] != '#' || msg.Content.Length < 2)
                return PlayingUsers[msg.Author].CurrentGame.OnMessage(msg, pos);
            
            var message = msg.Content.AsSpan();

            var whitespace = false;
            foreach (var c in message[1..])
            {
                pos++;
                if (char.IsWhiteSpace(c))
                {
                    whitespace = true;
                    continue;
                }

                if (whitespace)
                {
                    pos--;
                    break;
                }   
            }
            pos++;

            if (int.TryParse(message[1..pos], out var id))
            {
                if (PlayingUsers[msg.Author].Games.TryGetValue(id, out var game))
                {
                    PlayingUsers[msg.Author].CurrentGame = game;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"*You are not playing in game {message[..pos].Trim().ToString()}. You are playing game(s): {string.Join(", ", PlayingUsers[msg.Author].Games.Keys)}.*");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"*{message[..pos].Trim().ToString()} is not a valid game ID. You are playing game(s): {string.Join(", ", PlayingUsers[msg.Author].Games.Keys)}.*");
            }

            return message[pos..].Length != 0
                ? PlayingUsers[msg.Author].CurrentGame.OnMessage(msg, pos)
                : msg.Author.SendMessageAsync($"*You are now playing in game {message[..pos].Trim().ToString()}.*");
        }

        static void DropUser(object? sender, UserDroppingEventArgs e)
        {
            var game = (GameInstance) sender!;
            var gc = PlayingUsers[e.DroppingUser];
            gc.Games.TryRemove(game.Id, out _);

            if (gc.Games.Count == 0)
            { 
                PlayingUsers.TryRemove(e.DroppingUser, out _);
                e.DroppingUser.SendMessageAsync("You are in no more games. Thanks for playing with Leisure!");
            }
            else
            {
                gc.CurrentGame = gc.Games.Last().Value;
                e.DroppingUser.SendMessageAsync("You have been placed into game " + gc.CurrentGame.Id);
            }    

        }
        
        static void CloseGame(object? sender, EventArgs e)
        {
            var game = (GameInstance) sender!;
            foreach (var p in game.Users)
            {
                DropUser(game, new UserDroppingEventArgs(p));
            }
        }
    }
}