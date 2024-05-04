using System.Drawing;
using System.Text.RegularExpressions;
using DiscordMonies.Board;
using DiscordMonies.Remora;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;

namespace DiscordMonies.Game;

public static partial class Embeds
{
    private static readonly string[] Dies = [
        "1",
        "2",
        "3",
        "4",
        "5",
        "6"
    ];

    public static Embed PropertyObtained(GamePlayer player, Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Property Obtained")
            .WithDescription($"{player} has obtained {space} for {((ValueSpace) space.Info).Value.MoneyString()}")
            .Build().Get();
    }

    public static Embed SentToJail(GamePlayer victim)
    {
        return new EmbedBuilder()
            .WithTitle($"Jail {Emoji.OncomingPoliceCar}")
            .WithDescription($"**{victim}** has been sent to jail.")
            .WithColour(Color.Red)
            .Build().Get();
    }

    public static Embed CardDrawn(GamePlayer victim, bool isGamble, GameBoard board, LuckCard card)
    {
        var deck = isGamble ? "Gamble" : "Dubious Treasure";
        
        var humanReadableDescription = CardDescriptionRegex().Replace(card.Description, match =>
        {
            var type = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            var isInt = int.TryParse(value, out var val);

            if (type == "boardSpace")
            {
                return board.Spaces[val].Info.Name;
            }
            
            if (isInt)
            {
                return val.MoneyString();
            }

            if (value == "goSpace")
            {
                return board.PassGoValue.MoneyString();
            }

            return value;
        });

        
        return new EmbedBuilder()
            .WithTitle(deck)
            .WithDescription($"**{victim}** takes a deep breath and draws a {deck} card...")
            .WithColour(Color.Yellow)
            .AddField($"{deck} card", humanReadableDescription).Get()
            .Build().Get();
    }

    public static Embed TurnAdvanced(GamePlayer currentPlayer)
    {
        return new EmbedBuilder()
            .WithTitle("Next Round")
            .WithDescription($"It's **{currentPlayer}**'s turn.")
            .WithImageUrl("attachment://board.png")
            .AddField("Current Player", currentPlayer.ToString(), true).Get()
            .AddField("Current Position", currentPlayer.JailTurns != -1 ? "In Jail" : currentPlayer.CurrentSpace.NameWithPositionString, true).Get()
            .AddField("Player Colour", currentPlayer.Color.Name, true).Get()
            .Build().Get();
    }
    
    public static Embed TurnAdvancedDoubles(GamePlayer currentPlayer)
    {
        return new EmbedBuilder()
            .WithTitle("Next Round")
            .WithDescription($"It's **{currentPlayer}**'s turn again because they rolled doubles.")
            .WithImageUrl("attachment://board.png")
            .AddField("Current Player", currentPlayer.ToString(), true).Get()
            .AddField("Current Position", currentPlayer.CurrentSpace.NameWithPositionString, true).Get()
            .AddField("Player Colour", currentPlayer.Color.Name, true).Get()
            .Build().Get();
    }

    public static Embed DieRolled(GamePlayer player, GameDice dice, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Roll")
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]} and landed on **{landingSpace.NameWithPositionString}**")
            .Build().Get();
    }
    
    public static Embed JailDieRolled(GamePlayer player, GameDice dice, int attempts)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Roll")
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]}.")
            .AddField("Attempts Remaining", $"{attempts} attempts remaining").Get()
            .Build().Get();
    }

    [GeneratedRegex("{(moneyValue|boardSpace) ([^}]+)}")]
    private static partial Regex CardDescriptionRegex();

    public static Embed SpeedingDieRolled(GamePlayer player, GameDice dice)
    {
        return new EmbedBuilder()
            .WithTitle("Roll")
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]}. The policemen are on their way!")
            .Build().Get();
    }

    public static Embed FinalJailDieRolled(GamePlayer player, GameDice dice)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Roll")
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]}.")
            .AddField("Attempts Remaining", "0 attempts remaining.").Get()
            .Build().Get();
    }

    public static Embed JailDieDoublesRolled(GamePlayer player, GameDice dice, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Roll")
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]} and left jail. They landed on **{landingSpace.NameWithPositionString}**")
            .Build().Get();
    }

    public static Embed JailBail(GamePlayer player, int bailFine)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Fine")
            .WithDescription($"**{player}** has paid the fine of {bailFine.MoneyString()} and has been released from Jail")
            .Build().Get();
    }
    
    public static Embed JailBail(GamePlayer player, int bailFine, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Fine")
            .WithDescription($"**{player}** has paid the fine of {bailFine.MoneyString()} and has been released from Jail. They landed on **{landingSpace.NameWithPositionString}**")
            .Build().Get();
    }

    public static Embed JailBailCard(GamePlayer player)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Fine")
            .WithDescription($"**{player}** has used a Get Out of Jail Free card and has been released from Jail")
            .Build().Get();
    }

    public static Embed JailBailCard(GamePlayer player, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Fine")
            .WithDescription($"**{player}** has used a Get Out of Jail Free card and has been released from Jail. They landed on **{landingSpace.NameWithPositionString}**")
            .Build().Get();
    }
    
    public static Embed GenericLanding(GamePlayer player, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Player Moved")
            .WithDescription($"**{player}** landed on **{landingSpace.NameWithPositionString}**")
            .Build().Get();
    }

    public static Embed Mortgage(Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Mortgage")
            .WithDescription(
                $"**{space.Owner}** has mortgaged **{space.NameWithPositionString}** for {space.TitleDeed!.MortgageValue.MoneyString()}")
            .Build().Get();
    }

    public static Embed Unmortgage(Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Mortgage")
            .WithDescription(
                $"**{space.Owner}** has unmortgaged **{space.NameWithPositionString}** for {space.TitleDeed!.UnmortgageValue.MoneyString()}")
            .Build().Get();
    }

    public static Embed BankruptcyResolutionRequired(IEnumerable<GamePlayer> bankruptPlayers)
    {
        var gamePlayers = bankruptPlayers.ToList();
        if (gamePlayers.Count == 1)
        {
            return new EmbedBuilder()
                .WithTitle("Player is in arrears!")
                .WithDescription($"**{gamePlayers[0].DisplayName}** is in debt! Before the game can continue, they must clear their debt by selling houses, mortgaging or trading. If they are unable to clear their debt, they must declare bankruptcy.")
                .WithColour(Color.Red)
                .Build().Get();
        }
        
        return new EmbedBuilder()
            .WithTitle("Some players are in arrears!")
            .WithDescription($"{string.Join(", ", gamePlayers.Select(x => $"**{x.DisplayName}**"))} are in debt! Before the game can continue, they must clear their debt by selling houses, mortgaging or trading. If they are unable to clear their debt, they must declare bankruptcy.")
            .WithColour(Color.Red)
            .Build().Get();
    }

    public static Embed Dropped(GamePlayer player, GamePlayer? transferTo, IEnumerable<string> billOfMaterials)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Bankruptcy")
            .WithDescription($"**{player}** has been declared bankrupt and has been removed from the game. All of their remaining assets will be transferred to **{transferTo?.DisplayName ?? "the bank"}**.")
            .WithColour(Color.Red);

        var materials = billOfMaterials.ToList();
        if (materials.Count != 0)
        {
            builder = builder.AddField("Transferred Assets", string.Join("\n", materials)).Get();
        }
        
        return builder.Build().Get();
    }

    public static Embed Victory(GamePlayer player)
    {
        return new EmbedBuilder()
            .WithTitle("Victory!")
            .WithDescription($"The game has ended in a victory for **{player}**! The game is now closed.")
            .WithImageUrl("attachment://board.png")
            .WithColour(Color.LimeGreen)
            .Build().Get();
    }

    public static Embed MortgageTransfer(Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Mortgage Transferred")
            .WithDescription($"**{space}** was transferred, but is currently mortgaged. Do you want to keep the property mortgaged for 10% of the mortgage price, or unmortgage the property now?")
            .Build().Get();
    }

    public static Embed Auction(Space space, int value, GamePlayer? matterHighestBidder)
    {
        var embed = new EmbedBuilder()
            .WithTitle("It's auction time!")
            .WithDescription($"**{space}** is going for auction! The auction ends when all players except for one have dropped out!");

        if (value > 0)
        {
            embed = embed.AddField("Highest Bid", $"{value.MoneyString()} by **{matterHighestBidder}**").Get();
        }

        return embed.Build().Get();
    }

    public static Embed AuctionComplete(Space space, GamePlayer winner, int value)
    {
        return new EmbedBuilder()
            .WithTitle("Auction complete!")
            .WithDescription($"**{space}** was won by **{winner}** for {value.MoneyString()}")
            .Build().Get();
    }

    public static Embed AuctionFailure(Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Auction complete!")
            .WithDescription($"No one bid on **{space}**! It will be returned to the bank")
            .Build().Get();
    }
}