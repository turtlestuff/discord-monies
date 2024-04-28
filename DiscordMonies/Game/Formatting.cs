namespace DiscordMonies.Game;

internal static class Formatting
{
    public static string MoneyString(this int money, bool codeblock = true) => codeblock ? $"`{money:Ð#,0}`" : $"{money:Ð#,0}";
}
