using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Interactivity;

namespace DiscordMonies.Game;

public static class Buttons
{
    public static ButtonComponent Roll() =>
        new(ButtonComponentStyle.Primary,
            "Roll",
            CustomID: CustomIDHelpers.CreateButtonID("roll"));

    public static ButtonComponent Purchase(string space, int price) => 
        new(ButtonComponentStyle.Success, 
            $"Purchase {space} for {price.MoneyString(false)}", 
            CustomID: CustomIDHelpers.CreateButtonID("purchase"));

    public static ButtonComponent SendToAuction() =>
        new(ButtonComponentStyle.Danger, "Send to Auction",
            CustomID: CustomIDHelpers.CreateButtonID("auction"));
    
    public static ButtonComponent Bail(int bailPrice) => 
        new(ButtonComponentStyle.Danger, $"Bail for {bailPrice.MoneyString(false)}",
            CustomID: CustomIDHelpers.CreateButtonID("bail"));
}