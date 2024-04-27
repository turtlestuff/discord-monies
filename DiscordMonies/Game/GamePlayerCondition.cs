namespace DiscordMonies.Remora;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class RequiresPlayerAttribute(PlayerType playerType) : Attribute
{
    public PlayerType PlayerType { get; } = playerType;
}

internal enum PlayerType
{
    None, // Anyone in any channel
    PlayerOrSpectator, // Anyone in a game channel
    Player,
    CurrentPlayer
}
