# Introduction
Welcome to Discord Monies! The aim of the game is to build your empire and bankrupt your enemies!

# Getting Started
A few concepts to grasp:
- **Properties** are spaces located on the board (marked `A3`, `B6` etc.) They are the key to building your empire.
- **Money** is used to pay for properties, among other things. Each player starts with `Ð1,500` in cash.
- **Houses** and **Hotels** can be built on properties and increase the rent payable.

# Let's play!
Discord Monies is a turn based game, where everyone takes turns to roll the dice and move around the board. The first player will be prompted to roll the dice by issuing the `roll` command. Depending on which space they land on, a few different things can happen.

## Property Spaces
The most common type of space is a **Property space**. These can be identified with the colored banner along with the space identifier (`A3`, `B6` etc.)

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

# Development
etc. etc.

## Building a house

## Demolishing a house

## Mortgaging

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