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
        "<:1_:869985162979606598>",
        "<:2_:869985162971185212>",
        "<:3_:869985163793281104>",
        "<:4_:869985162962829422>",
        "<:5_:869985162979598346>",
        "<:6_:869985162740498473>"
    ];

    public static Embed PropertyObtained(GamePlayer player, Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Property Obtained")
            .WithDescription($"{player} has obtained {space} for {((ValueSpace) space.Info).Value.MoneyString()}.")
            .WithColour(space.GroupColorOrDefault(PrettyColors.Green))
            .Build().Get();
    }

    public static Embed SentToJail(GamePlayer victim)
    {
        return new EmbedBuilder()
            .WithTitle($"Jail {Emoji.OncomingPoliceCar}")
            .WithDescription($"**{victim}** has been sent to jail.")
            .WithColour(PrettyColors.Red)
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
            .WithColour(PrettyColors.Gold)
            .AddField($"{deck} card", humanReadableDescription).Get()
            .Build().Get();
    }

    public static Embed TurnAdvanced(GamePlayer currentPlayer)
    {
        return new EmbedBuilder()
            .WithTitle("Next Round")
            .WithDescription($"It's **{currentPlayer}**'s turn.")
            .WithImageUrl("attachment://board.png")
            .WithColour(currentPlayer.Color.Color)
            .AddField("Current Position", currentPlayer.JailTurns != -1 ? "In Jail" : currentPlayer.CurrentSpace.NameWithPositionString, true).Get()
            .AddField("Color", currentPlayer.Color.Name, true).Get()
            .AddField("Balance", currentPlayer.Money.MoneyString(), true).Get()
            .Build().Get();
    }
    
    public static Embed TurnAdvancedDoubles(GamePlayer currentPlayer)
    {
        return new EmbedBuilder()
            .WithTitle($"Doubles {Emoji.Die} {Emoji.Die}")
            .WithDescription($"It's **{currentPlayer}**'s turn again because they rolled doubles!")
            .WithImageUrl("attachment://board.png")
            .WithColour(PrettyColors.Green)
            .AddField("Current Position", currentPlayer.CurrentSpace.NameWithPositionString, true).Get()
            .AddField("Color", currentPlayer.Color.Name, true).Get()
            .AddField("Balance", currentPlayer.Money.MoneyString(), true).Get()
            .Build().Get();
    }

    public static Embed DieRolled(GamePlayer player, GameDice dice, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle($"Roll {Emoji.Die}")
            .WithColour(player.Color.Color)
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]} and landed on **{landingSpace.NameWithPositionString}**.")
            .Build().Get();
    }
    
    public static Embed JailDieRolled(GamePlayer player, GameDice dice, int attempts)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Roll")
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]}.")
            .WithColour(PrettyColors.Red)
            .AddField("Attempts Remaining", $"{attempts} attempts remaining").Get()
            .Build().Get();
    }

    [GeneratedRegex("{(moneyValue|boardSpace) ([^}]+)}")]
    private static partial Regex CardDescriptionRegex();

    public static Embed SpeedingDieRolled(GamePlayer player, GameDice dice)
    {
        return new EmbedBuilder()
            .WithTitle($"Roll {Emoji.Die}")
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]}. This is the third time they have rolled doubles. The policemen are on their way � no speeding!")
            .WithColour(player.Color.Color)
            .Build().Get();
    }

    public static Embed JailDieDoublesRolled(GamePlayer player, GameDice dice, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Roll")
            .WithDescription($"**{player}** rolled {Dies[dice.Die1 - 1]} {Dies[dice.Die2 - 1]} and was released from jail! They landed on **{landingSpace.NameWithPositionString}**.")
            .WithFooter("They do not get an extra turn for rolling doubles.")
            .WithColour(PrettyColors.Green)
            .Build().Get();
    }

    public static Embed JailBail(GamePlayer player, int bailFine)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Fine")
            .WithDescription($"**{player}** has paid the fine of {bailFine.MoneyString()} and was released from gaol.")
            .WithColour(player.Color.Color)
            .Build().Get();
    }
    
    public static Embed JailBail(GamePlayer player, int bailFine, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Fine")
            .WithDescription($"**{player}** has paid the fine of {bailFine.MoneyString()} and was released from jail. They landed on **{landingSpace.NameWithPositionString}**.")
            .WithColour(player.Color.Color)
            .Build().Get();
    }

    public static Embed JailBailCard(GamePlayer player)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Fine")
            .WithDescription($"**{player}** has used a Get Out of Jail Free card and was released from jail!")
            .WithColour(player.Color.Color)
            .Build().Get();
    }

    public static Embed JailBailCard(GamePlayer player, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Jail Fine")
            .WithDescription($"**{player}** has used a Get Out of Jail Free card and was released from jail! They landed on **{landingSpace.NameWithPositionString}**.")
            .WithColour(player.Color.Color)
            .Build().Get();
    }
    
    public static Embed GenericLanding(GamePlayer player, Space landingSpace)
    {
        return new EmbedBuilder()
            .WithTitle("Player Moved")
            .WithDescription($"**{player}** landed on **{landingSpace.NameWithPositionString}**.")
            .WithColour(player.Color.Color)
            .Build().Get();
    }

    public static Embed Mortgage(Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Mortgage")
            .WithDescription($"**{space.Owner}** has mortgaged **{space.NameWithPositionString}** for {space.TitleDeed!.MortgageValue.MoneyString()}.")
            .WithColour(space.GroupColorOrDefault(PrettyColors.Gold))
            .Build().Get();
    }

    public static Embed Unmortgage(Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Mortgage")
            .WithDescription(
                $"**{space.Owner}** has unmortgaged **{space.NameWithPositionString}** for {space.TitleDeed!.UnmortgageValue.MoneyString()}.")
            .WithColour(space.GroupColorOrDefault(PrettyColors.Gold))
            .Build().Get();
    }

    public static Embed BankruptcyResolutionRequired(GamePlayer bankruptPlayer, IList<RecoupFundsOperation> autoSellOperations)
    {
        return new EmbedBuilder()
            .WithTitle("Player is in arrears!")
            .WithDescription($"**{bankruptPlayer}** is in debt! Before the game can continue, they must clear their debt by selling houses, mortgaging, or trading.")
            .WithColour(PrettyColors.Red)
            .AddField("Auto-sell", "To get you out of debt, auto-sell will:\n" + Formatting.RecoupFundsOperations(autoSellOperations) + "\nIf you don't want to auto-sell, you can manually sell houses, mortgage, or trade to get out of debt.").Get()
            .Build().Get();
    }

    public static Embed Bankrupt(GamePlayer player, GamePlayer? transferTo)
    {
        return new EmbedBuilder()
            .WithTitle("Bankrupt!")
            .WithDescription($"**{player}** is in debt to **{transferTo?.ToString() ?? "the bank"}** and does not own sufficient assets to repay them!")
            .WithColour(PrettyColors.Red)
            .Build().Get();
    }

    public static Embed Dropped(GamePlayer player, GamePlayer? transferTo, IEnumerable<string> billOfMaterials)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Bankruptcy")
            .WithDescription($"**{player}** has declared bankruptcy and is now removed from the game. All of their remaining assets will be transferred to {$"**{transferTo?.DisplayName}**" ?? "**the bank**, where it will be auctioned off to the highest bidder"}.")
            .WithColour(PrettyColors.Red);

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
            .WithColour(PrettyColors.Green)
            .Build().Get();
    }

    public static Embed MortgageTransfer(Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Mortgage Transferred")
            .WithDescription($"**{space}** was transferred, but is currently mortgaged. Do you want to keep the property mortgaged for 10% of the mortgage price, or unmortgage the property now?")
            .WithColour(space.GroupColorOrDefault(PrettyColors.Green))
            .Build().Get();
    }

    public static Embed Auction(Space space, int value, GamePlayer? matterHighestBidder)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"It's auction time! {Emoji.Judge}")
            .WithDescription($"**{space}** is going for auction! The auction ends when all players except for one have withdrawn!")
            .WithColour(space.GroupColorOrDefault(Color.Transparent));

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
            .WithDescription($"**{space}** was won by **{winner}** for {value.MoneyString()}.")
            .WithColour(space.GroupColorOrDefault(Color.Transparent))
            .Build().Get();
    }

    public static Embed AuctionFailure(Space space)
    {
        return new EmbedBuilder()
            .WithTitle("Auction complete!")
            .WithDescription($"No one bid on **{space}**. The property will be returned to the bank.")
            .WithColour(space.GroupColorOrDefault(Color.Transparent))
            .Build().Get();
    }

    public static Embed NewAutoSellSituation(IList<RecoupFundsOperation> autoSellOperations)
    {
        return new EmbedBuilder()
            .WithTitle("Auto-sell")
            .WithDescription("Your situation has changed, so the previous auto-sell plan no longer applies. Auto-sell can get you out of debt by:\n" +
                             Formatting.RecoupFundsOperations(autoSellOperations))
            .Build().Get();
    }

    public static Embed AutoSellUnable()
    {
        return new EmbedBuilder()
            .WithTitle("Auto-sell")
            .WithDescription("Your situation has changed, and auto-sell is no longer able to get you out of debt.")
            .Build().Get();
    }

    public static Embed AutoSellComplete(GamePlayer player, IList<RecoupFundsOperation> autoSellOperations, int moneyEarned)
    {
        return new EmbedBuilder()
            .WithTitle("Auto-sell")
            .WithDescription($"Auto-sell performed the following actions and netted **{player}** {moneyEarned.MoneyString()}.")
            .AddField("Changes to properties", Formatting.RecoupFundsOperations(autoSellOperations)).Get()
            .Build().Get();
    }
}
