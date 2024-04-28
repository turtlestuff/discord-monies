using System.Text.RegularExpressions;
using DiscordMonies.Board;
using DiscordMonies.Remora;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;

namespace DiscordMonies.Game;

public partial class Embeds
{
    public static Embed PropertyObtained(GamePlayer player, Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Property Obtained")
            .WithDescription($"{player} has obtained {space} for {((ValueSpace) space.Info).Value.MoneyString()}")
            .Build().Get();
    }

    public static Embed CardDrawn(GamePlayer victim, bool isGamble, GameBoard board, LuckCard card)
    {
        var deck = isGamble ? "Gamble" : "Dubious Treasure";
        
        var humanReadableDescription = CardDescriptionRegex().Replace(card.Description, match =>
        {
            var type = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            var isInt = int.TryParse(value, out var val);

            if (type == "boardSpace")
            {
                return board.Spaces[val].Info.Name;
            }
            
            if (isInt)
            {
                return val.MoneyString();
            }

            if (value == "goSpace")
            {
                return board.PassGoValue.MoneyString();
            }

            return value;

        });

        
        return new EmbedBuilder()
            .WithTitle(deck)
            .WithDescription($"**{victim}** takes a deep breath and draws a {deck} card...")
            .AddField($"{deck} card", humanReadableDescription).Get()
            .Build().Get();
    }

    [GeneratedRegex("{(moneyValue|boardSpace) ([^}]+)}")]
    private static partial Regex CardDescriptionRegex();
}