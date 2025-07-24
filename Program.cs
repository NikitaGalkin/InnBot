using Microsoft.Extensions.Configuration;
using TelegramInnBot;

class Program
{
    static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("tokens.json")
            .Build();

        var token = config["TelegramBotToken"];

        var bot = new BotService(token);
        await bot.StartAsync();

        Console.WriteLine("Bot is launched. Press Enter for completion...");
        Console.ReadLine();
    }
}