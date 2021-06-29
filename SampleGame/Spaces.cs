using Discord;

namespace DiscordMoniesGame
{
    public record Space(string Name, SpaceBounds Bounds);
    public record GoToJailSpace(string Name, SpaceBounds Bounds) : Space(Name, Bounds);
    public record DrawCardSpace(string Name, CardType Type, SpaceBounds Bounds) : Space(Name, Bounds);
    public record GoSpace(string Name, int Value, SpaceBounds Bounds) : Space(Name, Bounds);
    public record TaxSpace(string Name, int Value, SpaceBounds Bounds) : Space(Name, Bounds);
    public record PropertySpace(string Name, int Value, IUser? Owner, bool Mortgaged, SpaceBounds Bounds) :
        Space(Name, Bounds);
    public record UtilitySpace(string Name, int Value, IUser? Owner, bool Mortgaged, SpaceBounds Bounds) : 
        PropertySpace(Name, Value, Owner, Mortgaged, Bounds);
    public record TrainStationSpace(string Name, int Value, IUser? Owner, bool Mortgaged, SpaceBounds Bounds) :
        PropertySpace(Name, Value, Owner, Mortgaged, Bounds);
    public record RoadSpace(string Name, int Value, int Group, IUser? Owner, bool Mortgaged, object Houses, SpaceBounds Bounds) :
        PropertySpace(Name, Value, Owner, Mortgaged, Bounds);


    public record struct SpaceBounds(int X, int Y, int Width, int Height);

    public enum CardType
    { 
        Chance,
        Chest
    }
}
