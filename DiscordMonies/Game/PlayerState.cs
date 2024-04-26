namespace DiscordMonies.Game;

public sealed class PlayerState
{
    public required int Money { get; set; }

    public string MoneyString() => $"`{Money:Ð#,0}`";
}