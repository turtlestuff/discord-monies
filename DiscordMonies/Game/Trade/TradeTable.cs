using DiscordMonies.Board;
using DiscordMonies.Remora;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;

namespace DiscordMonies.Game.Trade;

public record TradeTable
{
    public enum TradeTableState
    {
        Open,
        Offered,
        Closed
    }
    
    public required GamePlayer From { get; set; }
    
    public required GamePlayer To { get; set; }

    public List<IOwnable> Giving { get; set; } = [];
    
    public List<IOwnable> Receiving { get; set; } = [];
    
    // Positive: From -> To
    // Negative: To -> From
    public int Money { get; set; }

    public TradeTableState State { get; set; } = TradeTableState.Open;
    
    public required TradeTable? CopyOf { get; init; }

    public TradeTable Copy()
    {
        return new()
        {
            From = To,
            To = From,
            Money = -Money,
            Giving = Receiving.ToList(),
            Receiving = Giving.ToList(),
            CopyOf = this
        };
    }

    public Embed PrintTradeTable(bool asGiving)
    {
        return new EmbedBuilder()
            .WithTitle("Trade")
            .AddField($"{From} gives", string.Join("\n", Giving.Select(OwnableEntry).Append(Money > 0 ? Money.MoneyString() : string.Empty))).Get()
            .AddField($"{To} gives", string.Join("\n", Receiving.Select(OwnableEntry).Append(Money < 0 ? (-Money).MoneyString() : string.Empty))).Get()
            .WithFooter($"{Emoji.CircledM} indicates mortgaged properties, which if traded, will need to be immediately unmortgaged or a 10% mortgage transfer fee paid to the bank.")
            .Build().Get();
    }

    private static string OwnableEntry(IOwnable ownable)
    {
        return ownable is Space { IsMortgaged: true } ? $"{Emoji.CircledM} {ownable}" : ownable.ToString();
    }
}