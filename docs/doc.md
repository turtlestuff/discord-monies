# Introduction
Welcome to Discord Monies! The aim of the game is to build your empire and bankrupt your enemies!

# Getting Started
A few concepts to grasp:
- **Properties** are spaces located on the board (marked `A3`, `B6` etc.) They are the key to building your empire.
- **Money** is used to pay for properties, among other things. Each player starts with `Ð1,500` in cash.
- **Houses** and **Hotels** can be built on properties and increase the rent payable.

# Let's play!
Discord Monies is a turn based game, where everyone takes turns to roll the dice and move around the board. The first player will be prompted to roll the dice by issuing the `roll` command. Depending on which space they land on, a few different things can happen.

## Viewing Information
There are various informational commands you may use to gather information, such as:
- `bal`: views player's balance;
- `player`: views other information about the player, such as color of the piece, whether they own a Get Out of Jail Free card, their position on the board and the properties they own;
- `status`: views general information about the game, such as the round, the playing order along with the current player, the players' colors and available houses and hotels;
- `board`: views the board;
- `space [id]`: views information about that space, such as the type, the name, the value, the owner, the houses on the space, etc;
- `deed [id]`: views space's title deed.

For the commands relating to players, by default they will view your own information, however, a player's name may be appended to specify whose information to view.

## Property Spaces
The most common type of space is a **Property space**. There are various types of property spaces, the most common being the **Road space**, marked by a colored banner next to its identifier (`A3`, `B1`). There are also the **Train stations**, marked with the train station icon, and the two utilities, the **Water Works** and the **Electric Company**.

### If the property is unowned
The first person to land on a property space will be given the option to purchase it and add it to their portfolio. Each property has a set purchase price, payable upon purchase of the property.

If they decide not to purchase the property, the property will go to [Auction](#auctions).

### If the property is owned
If you land on an owned property, rent is payable (unless the property is [mortgaged](#mortgaging)). The amount of rent payable depends on the number of houses and is shown on the Title Deed for that property. Discord Monies will automatically calculate the rent payable and transfer the amount to the player that owns the property.

## Gamble and Dubious Treasure
Gamble and Dubious Treasure spaces require you to draw a card from the relevant set. Discord Monies will present a card to you and will process the instructions on that card. These cards can give you money, move you around the board, or do a number of other things!

## Tax Spaces
`A4` is the Income Tax space. `Ð200` is immediately payable to the bank upon landing on this square.

`D8` is the Luxury Tax space. `Ð100` is immediately payable to the bank upon landing on this square.

## Jail Spaces
By landing on the jail square in the top right corner, you are "just visitng" jail. Nothing happens on your turn.

Landing on the bottom left corner places you in [jail](#jail).

## Free Parking
Nothing happens when you land on Free Parking.

# Jail
There are three methods to get out of jail: Rolling doubles, paying the fine of `Ð50` using `bail` or using a Get Out of Jail Free card, which you may have collected after landing in a Gamble or Dubious Treasure space, with `usejailcard`. You only have 3 attempts to roll doubles and after that, you must bail or use your jail card.

# Development
Developlemnt is an important part to the game, as it allows you to increase the rent people have to pay when they land on your property. Development is done in **Road spaces**, and must be done equally along a color group.

## Building a house or hotel
Building a house is done with the `build [id]` command. It will build a house on the specified road space if it is possible to do so. Once you have reached 4 houses, building in that space will remove the houses and add a hotel. You can see the development of a space by looking in the top-left corner of a space (there will be a small house icon with 1-4 in it, or H for hotel) or using the `space [id]` command. The cost of building a house is displayed in the space's title deed.

Please note that there are a limited number of houses available. If there are no houses available, you must wait for someone to demolish a house or build a hotel to build a house yourself.

## Demolishing a house or hotel
To demolish a house, use `demolish [id]`. You will get half of the cost of building a house back for doing so.

## Mortgaging
If you are really tight on cash, you may mortgage a property. Mortgaging a property gives you half of its value back to you, but you will not receive rent payments for that space anymore. To mortgage a space, use `mortgage [id]`. Once you are able to, you may de-mortgage a property with `demortgage [id]`, and you will pay 110% of the mortgage value back to the bank.

**Tip:** the four commands described above can take more than 1 space ID, and the command will be executed for all those spaces, in sequence. In the case of `build` and `demolish`, you can even repeat spaces to develop them multiple steps at once (for example, assuming you own `A1` and `A3`, and that they have no houses on them, `build a1 a3 a1 a3 a1 a3` will bring them both to three houses).

# Trading
Trading works by laying out your own personal trade table, offering it to someone else, and then they may accept, reject, or copy it to their own trade table. The offer is then checked, and if it is valid, the items, properties and money will be traded.

## New trade
To lay out a new trade table, use `trade new`. If you already have a trade table laid out, you must `trade close` it first. You will get a confirmation message and will be shown your empty trade table.

## Configuring items
`trade give [item]` and `trade take [item]` add an item to the respective side of the trade table, or removes it if it is already present. `[item]` can be an amount of money, a Get Out of Jail Free card or a property space, with an option to keep it mortgaged or de-mortgage it.

If you wish to trade money, `[item]` must be a simple number, which will set the respective number. Unlike other items, it does not toggle. To remove an amount of money, you must use `0` as the amount. You cannot give and take money at the same time; `trade give [amount]` will set the giving amount, the same going for `trade take [amt]`.

If you wish to trade a Get Out of Jail Free card, use `jailcard` as the item.

To trade properties, `[item]` must be a location ID (i.e. `A3`, `B6`). You may also specify, after the location ID, whether to keep the property mortgaged, with `keep`, or de-mortgage it with `demortgage`. If it is not specified, the default is to keep it mortgaged.

## Offering, accepting, copying and rejecting
To offer a trade to another player, use `trade offer`. If a trade is ambiguous, you may specify who the recipient is with `trade offer [player]`. Otherwise, the recipient will be determined automatically. You will get a confirmation message with the index of the trade and the recipient will receive a message with the new trade table along with a message instructing them on what they may do: `trade accept [index]`, which will accept the trade, `trade reject [index]`, which will reject the trade, and `trade copy [index]`, which will copy the trade table into their own trade table, if there is space available (you may use `trade close` to delete a previous one), and reject the trade. The trade will only go through if both players own the respective assets.

## Recalling
If you wish to edit a trade that you or the recipient had rejected, you can use `trade recall [index]`, which will copy the trade table to your own.

# Auctions
Auction of a property will start when a player decides to auction off the property instead of buying it. The auction will start with the next player from them in the playing order, and will continue in the same manner, with the minimum bid of `Ð1`. 

On a player's auction turn they may `bid [amount]` to bid on the property or `skip` to skip the auction. **A player that has skipped cannot bid again**. Once all but one player has skipped, the auction is terminated and the property is sent to them for the proposed price. If all players skipped, the property is sent back to the bank.

# Bankruptcy
etc. etc.