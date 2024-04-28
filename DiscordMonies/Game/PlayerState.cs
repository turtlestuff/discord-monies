namespace DiscordMonies.Game;

public sealed class PlayerState
{
    public required int Money { get; set; }

    public string MoneyString() => Money.MoneyString();

    public int Position { get; set; } = 0;
}