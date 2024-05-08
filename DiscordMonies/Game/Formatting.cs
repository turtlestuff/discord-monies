namespace DiscordMonies.Game;

internal static class Formatting
{
    public static string MoneyString(this int money, bool codeblock = true) => 
        codeblock ? $"`{money:Ð#,0}`" : $"{money:Ð#,0}";

    public static string RecoupFundsOperations(IList<RecoupFundsOperation> operations)
    {
        var sellOperations = operations
            .OfType<SellImprovement>()
            .GroupBy(s => s.Space.NameWithPositionString)
            .Select(g => $"- Sell **{g.Count()}** houses on **{g.Key}**");

        var mortgageOperations = operations
            .OfType<MortgageProperty>()
            .Select(m => $"- Mortgage **{m.Space.NameWithPositionString}**");

        return string.Join('\n', sellOperations.Concat(mortgageOperations));
    }
}
