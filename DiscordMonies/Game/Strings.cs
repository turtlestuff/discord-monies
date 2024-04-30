namespace DiscordMonies.Game;

public class Strings
{
    public static string BalanceTooLow(string @for, int currentBalance, int requiredBalance) =>
        $"{@for}. You have {currentBalance.MoneyString()}. You need {requiredBalance.MoneyString()}";
    
    public static string UnableToDevelopOnColourSet(string colourSet) => $"You are currently unable to develop on the {colourSet} color set.";
}