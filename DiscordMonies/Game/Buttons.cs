using DiscordMonies.Board;
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

    public static ButtonComponent DeclareBankruptcy() => new(ButtonComponentStyle.Danger, $"Declare Bankruptcy",
        CustomID: CustomIDHelpers.CreateButtonID("declare-bankruptcy"));

    public static ButtonComponent UseJailCard(string type) =>
        new(ButtonComponentStyle.Success, "Use Get Out Of Jail Free Card",
            CustomID: CustomIDHelpers.CreateButtonIDWithState("use-jail-card", type));

    public static ButtonComponent TakeCard() =>
        new(ButtonComponentStyle.Secondary, "Take a card",
            CustomID: CustomIDHelpers.CreateButtonID("take-card"));

    public static ButtonComponent TakeCardPay(int price) =>
        new(ButtonComponentStyle.Secondary, $"Pay {price.MoneyString(false)}",
            CustomID: CustomIDHelpers.CreateButtonID("take-card-pay"));

    public static ButtonComponent AcceptTrade(string id) =>
        new(ButtonComponentStyle.Success, "Accept",
            CustomID: CustomIDHelpers.CreateButtonIDWithState("trade-accept", id));

    public static ButtonComponent CounterTrade(string id) =>
        new(ButtonComponentStyle.Secondary, "Counter",
            CustomID: CustomIDHelpers.CreateButtonIDWithState("trade-counter", id));

    public static ButtonComponent RejectTrade(string id) =>
        new(ButtonComponentStyle.Danger, "Reject",
            CustomID: CustomIDHelpers.CreateButtonIDWithState("trade-reject", id));

    public static ButtonComponent MortgageTransferUnmortgage(Space space) =>
        new(ButtonComponentStyle.Primary,
            $"Unmortgage {space} for {space.TitleDeed!.UnmortgageValue.MoneyString(false)}",
            CustomID: CustomIDHelpers.CreateButtonID("mortgage-transfer-unmortgage"));

    public static ButtonComponent MortgageTransferKeepMortgage(Space space) =>
        new(ButtonComponentStyle.Secondary,
            $"Keep {space} mortgaged for {((int)(space.TitleDeed!.MortgageValue * 0.1)).MoneyString(false)}",
            CustomID: CustomIDHelpers.CreateButtonID("mortgage-transfer-mortgage"));

    public static ButtonComponent DropAuction() =>
        new(ButtonComponentStyle.Danger, $"Drop out of this auction",
            CustomID: CustomIDHelpers.CreateButtonID("auction-drop"));

    public static ButtonComponent Bid(int value) =>
        new(ButtonComponentStyle.Primary, $"+{value.MoneyString(false)}",
            CustomID: CustomIDHelpers.CreateButtonIDWithState("auction-bid", $"{value}"));

    public static ButtonComponent AuctionTitleDeed() =>
        new(ButtonComponentStyle.Secondary, $"View Title Deed",
            CustomID: CustomIDHelpers.CreateButtonID("auction-title-deed"));
}