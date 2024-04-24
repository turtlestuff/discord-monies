namespace DiscordMonies.Remora;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class RequiresPlayerAttribute(PlayerType playerType) : Attribute
{
    public PlayerType PlayerType { get; } = playerType;
}

internal enum PlayerType
{
    None,
    PlayerOrSpectator,
    Player,
    CurrentPlayer
}
