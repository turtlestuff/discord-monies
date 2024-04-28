using DiscordMonies.Board;
using DiscordMonies.Remora;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;

namespace DiscordMonies.Game;

public class Embeds
{
    public static Embed PropertyObtained(GamePlayer player, Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Property Obtained")
            .WithDescription($"{player} has obtained {space} for {((ValueSpace) space.Info).Value.MoneyString()}")
            .Build().Get();
    }
}