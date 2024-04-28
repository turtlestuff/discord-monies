using System.Text.Json.Serialization;

namespace DiscordMonies.Board;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(BoardSpace), "Simple")]
[JsonDerivedType(typeof(DrawChestSpace), "Chest")]
[JsonDerivedType(typeof(DrawChanceSpace), "Chance")]
[JsonDerivedType(typeof(GoToJailSpace), "GoToJail")]
[JsonDerivedType(typeof(GoSpace), "Start")]
[JsonDerivedType(typeof(TaxSpace), "Tax")]
[JsonDerivedType(typeof(TrainStationSpace), "TrainStation")]
[JsonDerivedType(typeof(UtilitySpace), "Utility")]
[JsonDerivedType(typeof(RoadSpace), "Road")]
[JsonDerivedType(typeof(FreeParkingSpace), "FreeParking")]
public record BoardSpace
{
    public required string Name { get; init; }

    public required SpaceBounds Bounds { get; init; }
}

public sealed record GoToJailSpace : BoardSpace;
public sealed record FreeParkingSpace : BoardSpace;
public abstract record DrawCardSpace : BoardSpace;
public sealed record DrawChanceSpace : DrawCardSpace;
public sealed record DrawChestSpace : DrawCardSpace;

public abstract record ValueSpace : BoardSpace
{
    public required int Value { get; init; }
}

public sealed record GoSpace : ValueSpace;
public sealed record TaxSpace : ValueSpace;
public abstract record PropertySpace : ValueSpace;

public sealed record UtilitySpace : PropertySpace;
public sealed record TrainStationSpace : PropertySpace;
public sealed record RoadSpace : PropertySpace
{
    public required int Group { get; init; }
}

public readonly record struct SpaceBounds(int X, int Y, int Width, int Height);
