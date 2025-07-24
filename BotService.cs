using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using System.Text;
using TelegramInnBot.Models;
using System.Net.NetworkInformation;
using Newtonsoft.Json.Bson;
using System.Threading;

/// <summary>
/// Класс, инкапсулирующий логику бота.
/// </summary>
public class BotService
{
    private readonly TelegramBotClient _botClient;
    private string[]? _lastCommand; // Последняя команда для /last.

    // Конструктор, инициализирующий АПИ бота.
    public BotService(string token)
    {
        _botClient = new TelegramBotClient(token);
    }

    // Настройка и подпись на делегат АПИ.
    public async Task StartAsync()
    {
        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await _botClient.GetMeAsync();
        Console.WriteLine($"Bot @{me.Username} has launched.");
    }

    // Ответ на команду "/start".
    private async Task HandleStartCommand(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(chatId, "Привет! Я бот, который выдает информацию о компаниях по ИНН.", cancellationToken: cancellationToken);
    }

    // Ответ на команду "/help".
    private async Task HandleHelpCommand(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(chatId,
                    "/start – начать общение\n" +
                    "/help – список команд\n" +
                    "/hello – информация о разработчике\n" +
                    "/inn <ИНН...> – найти компании\n" +
                    "/last – повторить последнее действие",
                    cancellationToken: cancellationToken);
    }

    // Ответ на команду "/hello".
    private async Task HandleHelloCommand(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(chatId,
            "ФИО: Галкин Никита Сергеевич\nEmail: nik.gals01@gmail.com\nGitHub: https://github.com/NikitaGalkin\nРезюме: https://hh.ru/resume/0e7bded3ff0de19db20039ed1f59374a7a4474",
            cancellationToken: cancellationToken);
    }

    // Ответ на некорректную команду "/inn".
    private async Task HandleInnIncorrectCommand(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(chatId, "Укажите ИНН после команды /inn. Можно несколько через пробел.", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Ответ на команду "/inn".
    /// </summary>
    /// <param name="command"> Обрабатываемая команда пользователя. </param>
    private async Task HandleInnCommand(ITelegramBotClient botClient, long chatId, string[] command, CancellationToken cancellationToken)
    {

        var inns = command.Skip(1).ToList();
        var companies = await InnService.GetCompaniesAsync(inns);

        var sorted = companies.OrderBy(c => c.Name).ToList();

        var sb = new StringBuilder();

        for (int i = 0; i < sorted.Count; ++i)
        {
            sb.AppendLine(((sorted.Count == 1) ? "" : $"{i + 1}) ") + $"{sorted[i].Name}" + ((sorted[i].Address == "") ? "" : $" — {sorted[i].Address}"));
        }

        await botClient.SendTextMessageAsync(chatId, sb.ToString(), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Логика обработки команды.
    /// </summary>
    /// <param name="command"> Обрабатываемая команда пользователя. </param>
    Func<Task> SelectCommand(ITelegramBotClient botClient, long chatId, string[] command, CancellationToken cancellationToken) 
        => command[0].ToLower() switch
    {
        "/start" => () => HandleStartCommand(botClient, chatId, cancellationToken),

        "/help" => () => HandleHelpCommand(botClient, chatId, cancellationToken),

        "/hello" => () => HandleHelloCommand(botClient, chatId, cancellationToken),

        "/inn" => command.Length < 2
            ? () => HandleInnIncorrectCommand(botClient, chatId, cancellationToken)
            : () => HandleInnCommand(botClient, chatId, command, cancellationToken),

        "/last" => _lastCommand == null
            ? () => botClient.SendTextMessageAsync(
                chatId,
                "Нет предыдущей команды.",
                cancellationToken: cancellationToken)
            : () => SelectCommand(botClient, chatId, _lastCommand, cancellationToken)(),

        _ => () => botClient.SendTextMessageAsync(chatId, "Неизвестная команда. Введите /help.", cancellationToken: cancellationToken)
    };

    /// <summary>
    /// Обработка сообщения пользователя по подписи на делегат.
    /// </summary>
    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } messageText }) return;

        var chatId = update.Message.Chat.Id;
        var command = messageText.Trim().Split(' ');

        // Сохраняем только если это НЕ /last
        if (command[0].ToLower() != "/last")
            _lastCommand = command;

        var execute = SelectCommand(botClient, chatId, command, cancellationToken);
        await execute();
    }

    // Обработка ошибки по подписи на делегат.
    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error: {exception.Message}");
        return Task.CompletedTask;
    }
}