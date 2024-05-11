using System.ComponentModel;
using DiscordMonies.Board;
using DiscordMonies.Remora;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace DiscordMonies.Game.Trade;

[Group("trade")]
public class TradeCommands(GameState gameState, IInteractionCommandContext ctx, IFeedbackService feedback, TradeService tradeService) : CommandGroup
{
    [Command("open")]
    [RequiresPlayer(PlayerType.Player)]
    [Description("Set up a new trade with a player")]
    [Ephemeral]
    public async Task<Result> Open([Description("The player to trade with")] GamePlayer otherPlayer)
    {
        var player = gameState.GetById(ctx.GetUserId());
        if (otherPlayer == player)
        {
            await feedback.SendContextualAsync("You can't trade with yourself.");
            return Result.Success;
        }
        
        // Open a new trade table and set it to the player
        player.ConfiguringTradeTable = new()
        {
            From = player,
            To = otherPlayer,
            CopyOf = null
        };
        
        await feedback.SendContextualAsync($"Opened a new trade table. Add items to the trade table, and then offer it to {Mention.User(otherPlayer)} when you're ready.");
        return Result.Success;
    }

    [Group("give")]
    public class GiveCommands(TradeService tradeService) : CommandGroup
    {

        [Command("property")]
        [RequiresPlayer(PlayerType.Player)]
        [RequiresTradeTable]
        [Description("Place one of your properties on the trade table")]
        [Ephemeral]
        public async Task<Result> GiveProperty([AutocompleteProvider("autocomplete::spaces:owned")] Space space)
        {
            await tradeService.Toggle(space, true);
            return Result.Success;
        }

        [Command("money")]
        [RequiresPlayer(PlayerType.Player)]
        [RequiresTradeTable]
        [Description("Place your money on the trade table")]
        [Ephemeral]
        public async Task<Result> GiveMoney(int amount)
        {
            await tradeService.SetMoney(amount, true);
            return Result.Success;
        }
    }

    [Group("take")]
    public class TakeCommands(TradeService tradeService) : CommandGroup
    {
        [Command("property")]
        [RequiresPlayer(PlayerType.Player)]
        [RequiresTradeTable]
        [Description("Place one of the other party's properties on the trade table")]
        [Ephemeral]
        public async Task<Result> TakeProperty([AutocompleteProvider("autocomplete::spaces:trade")] Space space)
        {
            await tradeService.Toggle(space, false);
            return Result.Success;
        }

        [Command("money")]
        [RequiresPlayer(PlayerType.Player)]
        [RequiresTradeTable]
        [Description("Place the other party's money on the trade table")]
        [Ephemeral]
        public async Task<Result> TakeMoney(int amount)
        {
            await tradeService.SetMoney(amount, false);
            return Result.Success;
        }
    }

    
    [Command("offer")]
    [RequiresPlayer(PlayerType.Player)]
    [Description("Offer the trade table to the other player")]
    [Ephemeral]
    public async Task<Result> Offer()
    {
        await tradeService.Offer();
        return Result.Success;
    }
}

public class TradeInteractions(GameState gameState, TradeService tradeService) : InteractionGroup
{
    [Button("trade-accept")]
    [RequiresPlayer(PlayerType.Player)]
    [Ephemeral]
    public async Task<Result> Accept(int state)
    {
        var tradeTable = gameState.TradeTables[state];
        await tradeService.AcceptTradeTable(tradeTable);
        return Result.Success;
    }
    
    [Button("trade-counter")]
    [RequiresPlayer(PlayerType.Player)]
    [Ephemeral]
    public async Task<Result> Counter(int state)
    {
        var tradeTable = gameState.TradeTables[state];
        await tradeService.CounterTradeTable(tradeTable);
        return Result.Success;
    }
    
    [Button("trade-reject")]
    [RequiresPlayer(PlayerType.Player)]
    [Ephemeral]
    public async Task<Result> Reject(int state)
    {
        var tradeTable = gameState.TradeTables[state];
        await tradeService.RejectTradeTable(tradeTable);
        return Result.Success;
    }
}