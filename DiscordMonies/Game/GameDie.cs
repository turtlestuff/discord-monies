namespace DiscordMonies.Game;

public class GameDie
{
    public int Die1 { get; set; } = 1;

    public int Die2 { get; set; } = 1;
    
    public bool BothDiesHaveSameValue => Die1 == Die2 && !WasReset;

    public int SpeedingCount { get; private set; }
    
    public int Total => Die1 + Die2;

    private bool WasReset { get; set; } = true;

    public int Roll()
    {
        WasReset = false;
        SpeedingCount++;
        
        Die1 = Random.Shared.Next(6) + 1;
        Die2 = Random.Shared.Next(6) + 1;

        Die1 = 1;
        Die2 = 2;
        
        return Die1 + Die2;
    }

    public void Reset()
    {
        WasReset = true;
        SpeedingCount = 0;
    }
    
    public static implicit operator int(GameDie d)
    {
        return d.Die1 + d.Die2;
    }
}