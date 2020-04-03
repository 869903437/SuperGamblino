﻿using System;
using System.Threading.Tasks;
using System.Timers;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SuperGamblino.GameObjects;
using SuperGamblino.Helpers;

namespace SuperGamblino.Commands
{
    class SlotsCommand
    {
        private readonly Config _config;
        private readonly Database _database;
        private readonly Messages _messages;
        private readonly BetSizeParser _betSizeParser;

        public SlotsCommand(Database database, Config config, Messages messages, BetSizeParser betSizeParser)
        {
            _database = database;
            _config = config;
            _messages = messages;
            _betSizeParser = betSizeParser;
        }

        [Command("slots")]
        [Aliases("slot")]
        [Cooldown(1, 2, CooldownBucketType.User)]
        [Description("Basic slots command!")]
        public async Task OnExecute(CommandContext command)
        {
            try
            {
                var argument = command.RawArgumentString.ToUpper().TrimStart().Split(' ');
                if (argument.Length == 1)
                {
                    int bet = -1;
                    if (argument[0].Trim().ToLower() == "all")
                    {
                        bet = await _database.CommandGetUserCredits(command.User.Id);
                    }
                    else if (argument[0].Trim().ToLower() == "half")
                    {
                        bet = await _database.CommandGetUserCredits(command.User.Id) / 2;
                    }
                    else
                    {
                        bet = _betSizeParser.Parse(argument[0]);
                    }
                    if (bet == -1)
                    {
                        throw new NotImplementedException(); //Handle if bet is given in wrong format
                    }
                    if (await _database.CommandSubsctractCredits(command.User.Id, bet))
                    {
                        //win or lose here
                        bool hasWon = false;

                        Slots.SlotsResult result = Slots.GetResult(command.Client);

                        int pointsResult = Slots.GetPointsFromResult(result, bet);

                        string message = "";

                        if (pointsResult > 0 && pointsResult >= bet)
                        {
                            hasWon = true;
                            if (Slots.IsJackpot(result))
                            {
                                message = "\n" + "JACKPOT!!! You won " + pointsResult + " points!";
                            }
                            else if (Slots.IsDouble(result))
                            {
                                message = "\n" + "DOUBLE! You won " + pointsResult + " points!";
                            }
                            await _database.CommandGiveCredits(command.User.Id, pointsResult - bet);
                        }

                        //end result
                        Exp expHelper = new Exp(_database);
                        var expResult = await expHelper.Give(command, bet);
                        if (expResult.DidUserLevelUp) await _messages.LevelUp(command);
                        DiscordEmbed resultMsg = new DiscordEmbedBuilder
                        {
                            Color = new DiscordColor(_config.ColorSettings.Info),
                            Description = "Result: " + result.EmojiOne + " " + result.EmojiTwo + " " + result.EmojiThree
                            + message
                        };
                        await command.RespondAsync("", false, resultMsg);

                        await _database.AddGameHistory(command.User.Id, new GameHistory()
                        {
                            GameName = "slots",
                            HasWon = hasWon,
                            CoinsDifference = hasWon ? bet : bet * -1
                        });
                        if (hasWon == true)
                            await _messages.Won(command, bet, expResult);
                        else
                            await _messages.Lost(command);
                    }
                    else
                    {
                        await _messages.NotEnoughCredits(command);
                    }
                }
                else
                {
                    await _messages.InvalidArgument(command, new[] { "<Bet>" });
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "Object reference not set to an instance of an object.")
                {
                    await _messages.InvalidArgument(command, new[] { "<Bet>" });
                }
                else if (ex.Message == "Input string was not in a correct format.")
                {
                    DiscordEmbed message = new DiscordEmbedBuilder
                    {
                        Color = new DiscordColor(_config.ColorSettings.Warning),
                        Description = "Bets can only be whole numbers."
                    };
                    await command.RespondAsync("", false, message);
                }
                else
                {
                    Console.WriteLine(ex);
                    DiscordEmbed message = new DiscordEmbedBuilder
                    {
                        Color = new DiscordColor(_config.ColorSettings.Warning),
                        Description = "This is unexpected..."
                    };
                    await command.RespondAsync("", false, message);
                }
            }
        }
    }
}
