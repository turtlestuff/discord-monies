namespace DiscordMonies.Game;

internal static class Formatting
{
    public static string MoneyString(this int money) => $"`{money:Ð#,0}`";
}
