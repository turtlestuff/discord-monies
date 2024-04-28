namespace DiscordMonies.Game;

public class GameDie
{
    public int Die1 { get; set; } = 1;

    public int Die2 { get; set; } = 1;
    
    public bool BothDiesHaveSameValue => Die1 == Die2;

    public int SpeedingCount { get; private set; }

    public int Roll()
    {
        SpeedingCount++;
        
        Die1 = Random.Shared.Next(6) + 1;
        Die2 = Random.Shared.Next(6) + 1;

        return Die1 + Die2;
    }

    public void Reset()
    {
        SpeedingCount = 0;
    }
    
    public static implicit operator int(GameDie d)
    {
        return d.Die1 + d.Die2;
    }
}