using DiscordMonies.Board;
using DiscordMonies.Game;
using DiscordMonies.Remora;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;

namespace DiscordMonies.Extensions;

public static class SpaceExtensions
{
    public static Embed TitleDeedEmbed(this Space space)
    {
        if (space.Info is not ValueSpace valueSpace)
        {
            throw new InvalidOperationException();
        }
        
        // TODO: handle stations
        
        return new EmbedBuilder()
            .WithTitle($"Title Deed for {space} (colour set)")
            .WithDescription($"Value: {valueSpace.Value.MoneyString()}")
            .AddField("Rent Values", $"""
                                      RENT: {space.TitleDeed!.RentValues[0].MoneyString()}
                                      *Rent doubled when entire group is owned*
                                      With **1 house**: {space.TitleDeed.RentValues[1].MoneyString()}
                                      With **2 house**: {space.TitleDeed.RentValues[2].MoneyString()}
                                      With **3 house**: {space.TitleDeed.RentValues[3].MoneyString()}
                                      With **4 house**: {space.TitleDeed.RentValues[4].MoneyString()}
                                      With **Hotel**: {space.TitleDeed.RentValues[5].MoneyString()}
                                      """).Get()
            .AddField("House Cost", $"{space.TitleDeed.HouseCost.MoneyString()}", true).Get()
            .AddField("Hotel Cost", $"{space.TitleDeed.HotelCost.MoneyString()}", true).Get()
            .AddField("Mortgage Value", $"{space.TitleDeed.MortgageValue.MoneyString()}", true).Get()
            .Build().Get();
    }
}