using DiscordMonies.Board;
using DiscordMonies.Game.UnresolvedMatters;
using DiscordMonies.Remora;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;

namespace DiscordMonies.Game.Trade;

public class TradeService(IFeedbackService feedback, IInteractionContext ctx, GameState gameState, GameControllerService gameControllerService)
{
    public async Task Toggle(IOwnable ownable, bool giving)
    {
        var player = gameState.GetById(ctx.GetUserId());
        var tradeTable = player.ConfiguringTradeTable!;

        var entry = giving ? tradeTable.Giving : tradeTable.Receiving;
        var owner = giving ? tradeTable.From : tradeTable.To;
        
        // Always process removal from the trade table, even if it is technically not valid because trading is asynchronous.
        if (entry.Remove(ownable))
        {
            await feedback.SendContextualAsync($"Removed {ownable} from the trade table.");
            return;
        }
        
        switch (ownable)
        {
            case Space space:
                if (space.Info is not PropertySpace)
                {
                    await feedback.SendContextualAsync("You can't trade that space.");
                    return;
                }

                if (space.Info is RoadSpace rs)
                {
                    var otherSpaces = gameControllerService.SpacesInColorSet(new(rs.Group));
                    if (otherSpaces.Any(x => x.BuiltHouses > 0))
                    {
                        await feedback.SendContextualAsync($"{space} can't be traded because it is in a set with houses. Demolish the houses before trading the property.");
                        return;
                    }
                }
                
                break;
        }

        if (ownable.Owner != owner)
        {
            if (owner == player)
            {
                await feedback.SendContextualAsync($"Unable to add **{ownable}** to the trade table because you don't own it.");
            }
            else
            {
                await feedback.SendContextualAsync($"Unable to add **{ownable}** to the trade table because **{owner}** doesn't own it.");
            }
            return;
        }
        
        entry.Add(ownable);
        await feedback.SendContextualAsync($"Added {ownable} to the trade table.", embeds: new([tradeTable.PrintTradeTable(true)]));
    }

    public async Task SetMoney(int money, bool giving)
    {
        var player = gameState.GetById(ctx.GetUserId());
        var tradeTable = player.ConfiguringTradeTable!;
        var givingMoney = giving ? tradeTable.From : tradeTable.To;

        if (givingMoney.Money < money)
        {
            if (givingMoney == player)
            {
                await feedback.SendContextualAsync($"Unable to add {money.MoneyString()} to the trade table because you don't have enough money. You have {givingMoney.MoneyString()}.");
            }
            else
            {
                await feedback.SendContextualAsync($"Unable to add {money.MoneyString()} to the trade table because **{givingMoney}** doesn't have enough money. They have {givingMoney.MoneyString()}.");
            }
            return;
        }
        
        if (giving)
        {
            tradeTable.Money = money;
        }
        else
        {
            tradeTable.Money = -money;
        }
        await feedback.SendContextualAsync($"Added {money.MoneyString()} to the trade table.", embeds: new([tradeTable.PrintTradeTable(true)]));
    }

    public async Task Offer()
    {
        var player = gameState.GetById(ctx.GetUserId());
        var tradeTable = player.ConfiguringTradeTable!;

        if (tradeTable is { Money: 0, Giving.Count: 0, Receiving.Count: 0 })
        {
            await feedback.SendContextualAsync(
                $"Unable to offer this trade table as it is empty. Please add items to the trade table.",
                embeds: new([tradeTable.PrintTradeTable(true)]));
            return;
        }
        
        if (!await EnsureTradeTableValid(tradeTable))
        {
            await feedback.SendContextualAsync(
                $"Unable to offer this trade table as it is now invalid. Please check the trade table and ensure that each player has the required assets in order to trade.",
                embeds: new([tradeTable.PrintTradeTable(true)]));
            return;
        }

        await feedback.SendContextualAsync("Offering trade table...");

        player.ConfiguringTradeTable = null;
        tradeTable.State = TradeTable.TradeTableState.Offered;

        var id = gameState.TradeTables.Count.ToString();
        gameState.TradeTables.Add(tradeTable);

        await feedback.SendAsync(gameState.ThreadId,
            tradeTable.CopyOf is not null ? $"{Mention.User(tradeTable.To)}, **{tradeTable.From}** offers you a counter trade." : $"{Mention.User(tradeTable.To)}, **{tradeTable.From}** wants to trade with you.",
            embeds: new([tradeTable.PrintTradeTable(false)]), options: new()
            {
                MessageComponents = new([
                    new ActionRowComponent([
                        Buttons.AcceptTrade(id),
                        Buttons.CounterTrade(id),
                        Buttons.RejectTrade(id)
                    ])
                ])
            });
    }

    public Task<bool> EnsureTradeTableValid(TradeTable tradeTable)
    {
        // Ensure everyone has enough money
        if (tradeTable.Money < 0)
        {
            if (tradeTable.To.Money < -tradeTable.Money)
            {
                return Task.FromResult(false);
            }
        }
        else
        {
            if (tradeTable.From.Money < tradeTable.Money)
            {
                return Task.FromResult(false);
            }
        }

        if (tradeTable.Receiving.Any(o => o.Owner != tradeTable.To))
        {
            return Task.FromResult(false);
        }
        
        if (tradeTable.Giving.Any(o => o.Owner != tradeTable.From))
        {
            return Task.FromResult(false);
        }
        
        return Task.FromResult(true);
    }

    public async Task AcceptTradeTable(TradeTable tradeTable)
    {
        if (tradeTable.State != TradeTable.TradeTableState.Offered)
        {
            await feedback.SendContextualAsync("You can't accept this trade table because it's not currently open.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return;
        }
        
        var player = gameState.GetById(ctx.GetUserId());
        if (tradeTable.To != player)
        {
            await feedback.SendContextualAsync("You can't accept this trade table because it's not addressed to you.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return;
        }
        
        if (!await EnsureTradeTableValid(tradeTable))
        {
            await feedback.SendContextualAsync(
                "Unable to accept this trade table as it is now invalid. Please check the trade table and ensure that each player has the required assets in order to trade. You can counter the trade table in order to edit it.",
                embeds: new([tradeTable.PrintTradeTable(true)]));
            return;
        }
        
        tradeTable.State = TradeTable.TradeTableState.Closed;
        
        // Perform the trade!
        gameState.UnresolvedMatters.AddRange(tradeTable.Giving.Select(o => o.GiveTo(tradeTable.To)).OfType<IUnresolvedMatter>());
        gameState.UnresolvedMatters.AddRange(tradeTable.Receiving.Select(o => o.GiveTo(tradeTable.From)).OfType<IUnresolvedMatter>());

        await gameControllerService.TryPay(tradeTable.From, tradeTable.To, tradeTable.Money);

        await feedback.SendAsync(gameState.ThreadId,
            $"{tradeTable.To} has completed a trade with **{tradeTable.From}**.",
            embeds: new([tradeTable.PrintTradeTable(true)]));
    }

    public async Task CounterTradeTable(TradeTable tradeTable)
    {
        if (tradeTable.State != TradeTable.TradeTableState.Offered)
        {
            await feedback.SendContextualAsync("You can't counter this trade table because it's not currently open.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return;
        }
        
        var player = gameState.GetById(ctx.GetUserId());
        if (tradeTable.To != player)
        {
            await feedback.SendContextualAsync("You can't counter this trade table because it's not addressed to you.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return;
        }
        
        tradeTable.State = TradeTable.TradeTableState.Closed;
        player.ConfiguringTradeTable = tradeTable.Copy();
        
        await feedback.SendContextualAsync("You can now edit the trade. Once you're done, offer the trade with /trade offer again.",
            embeds: new([tradeTable.PrintTradeTable(true)]), options: new()
        {
            MessageFlags = new(MessageFlags.Ephemeral)
        });
    }

    public async Task RejectTradeTable(TradeTable tradeTable)
    {
        if (tradeTable.State != TradeTable.TradeTableState.Offered)
        {
            await feedback.SendContextualAsync("You can't reject this trade table because it's not currently open.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return;
        }
        
        var player = gameState.GetById(ctx.GetUserId());
        if (tradeTable.To != player)
        {
            await feedback.SendContextualAsync("You can't reject this trade table because it's not addressed to you.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return;
        }
        
        tradeTable.State = TradeTable.TradeTableState.Closed;
        await feedback.SendAsync(gameState.ThreadId,
            $"{Mention.User(tradeTable.From)}, **{tradeTable.To}** rejected your trade.");
    }
}