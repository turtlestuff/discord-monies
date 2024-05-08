using DiscordMonies.Board;
using DiscordMonies.Game;
using DiscordMonies.Remora;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;

using System.Drawing;

namespace DiscordMonies.Extensions;

public static class SpaceExtensions
{
    public static Embed TitleDeedEmbed(this Space space)
    {
        if (space.Info is not ValueSpace valueSpace)
        {
            throw new InvalidOperationException();
        }

        return valueSpace switch
        {
            RoadSpace roadSpace => new EmbedBuilder()
                .WithTitle($"Title Deed for {space} ({space.Board.GroupNames[roadSpace.Group]})")
                .WithDescription($"Value: {valueSpace.Value.MoneyString()}")
                .WithColour(space.GroupColorOrDefault(Color.Transparent))
                .WithImageUrl("attachment://board.png")
                .AddField("Rent Values", $"""
                                          **RENT**: {space.TitleDeed!.RentValues[0].MoneyString()}
                                          *Rent doubled when the entire group is owned by one person*
                                          With **1 house**: {space.TitleDeed.RentValues[1].MoneyString()}
                                          With **2 houses**: {space.TitleDeed.RentValues[2].MoneyString()}
                                          With **3 houses**: {space.TitleDeed.RentValues[3].MoneyString()}
                                          With **4 houses**: {space.TitleDeed.RentValues[4].MoneyString()}
                                          With **Hotel**: {space.TitleDeed.RentValues[5].MoneyString()}
                                          """).Get()
                .AddField("House Cost", $"{space.TitleDeed.HouseCost.MoneyString()}", true).Get()
                .AddField("Hotel Cost", $"{space.TitleDeed.HotelCost.MoneyString()}", true).Get()
                .AddField("Mortgage Value", $"{space.TitleDeed.MortgageValue.MoneyString()}", true).Get()
                .Build().Get(),
            TrainStationSpace => new EmbedBuilder()
                .WithTitle($"Title Deed for {space}")
                .WithDescription($"Value: {valueSpace.Value.MoneyString()}")
                .WithImageUrl("attachment://board.png")
                .AddField("Rent Values", $"""
                                          If 1 T.S. is owned: {space.TitleDeed!.RentValues[0].MoneyString()}
                                          If 2 T.S. are owned: {(space.TitleDeed!.RentValues[0] << 1).MoneyString()}
                                          If 3 T.S. are owned: {(space.TitleDeed!.RentValues[0] << 2).MoneyString()}
                                          If 4 T.S. are owned: {(space.TitleDeed!.RentValues[0] << 3).MoneyString()}
                                          """).Get()
                .AddField("Mortgage Value", $"{space.TitleDeed.MortgageValue.MoneyString()}", true).Get()
                .Build().Get(),
            UtilitySpace => new EmbedBuilder()
                .WithTitle($"Title Deed for {space}")
                .WithDescription($"Value: {valueSpace.Value.MoneyString()}")
                .WithImageUrl("attachment://board.png")
                .AddField("Rent Values", $"""
                                          If one utility is owned, the rent is **4×** the value on the dice.
                                          If both are owned, it is **10×** the value on the dice.
                                          """).Get()
                .AddField("Mortgage Value", $"{space.TitleDeed!.MortgageValue.MoneyString()}", true).Get()
                .Build().Get(),
            _ => throw new ArgumentOutOfRangeException(nameof(space))
        };
    }
}