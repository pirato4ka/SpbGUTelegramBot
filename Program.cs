using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Collections.Generic;

namespace MafiaRegistrationBot
{
    class Program
    {
        private static TelegramBotClient? botClient;
        private static DatabaseService? dbService;
        private static string botToken = "7808203791:AAEs0Gk0doVojmo0h0lZ_NwKd5KkNRyRONo";

        // Для отслеживания состояния регистрации пользователей
        private static Dictionary<long, RegistrationState> userStates = new Dictionary<long, RegistrationState>();

        static async Task Main(string[] args)
        {
            botClient = new TelegramBotClient(botToken);
            dbService = new DatabaseService();


            var me = await botClient.GetMeAsync();
            Console.WriteLine($"Бот @{me.Username} запущен!");

            using var cts = new CancellationTokenSource();

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот слушает сообщения...");
            Console.WriteLine("Нажмите Enter для остановки");
            Console.ReadLine();

            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
        {
            if (update.Message is not { } message || message.Text is not { } text)
                return;

            var chatId = message.Chat.Id;
            var userId = message.From.Id;
            var userName = message.From.FirstName;

            Console.WriteLine($"Сообщение от {userName}: {text}");

            try
            {
                if (text == "/start")
                {
                    await client.SendTextMessageAsync(chatId, 
                        $"Привет, {userName}! Я бот для записи в мафию.\n" +
                        "Используй команды:\n" +
                        "/register - записаться на игру\n" +
                        "/list - список участников\n" +
                        "/unregister - отменить запись", 
                        cancellationToken: token);
                }
                else if (text == "/register")
                {
                    await HandleRegistrationStart(client, chatId, userId, userName, token);
                }
                else if (text == "/list")
                {
                    await HandlePlayerList(client, chatId, token);
                }
                else if (text == "/unregister")
                {
                    await HandleUnregister(client, chatId, userId, userName, token);
                }
                else if (userStates.ContainsKey(userId))
                {
                    await HandleRegistrationStep(client, chatId, userId, text, token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки: {ex.Message}");
            }
        }

        private static async Task HandleRegistrationStart(ITelegramBotClient client, long chatId, long userId, string userName, CancellationToken token)
        {
            if (dbService.IsPlayerRegistered(userId))
            {
                await client.SendTextMessageAsync(chatId, 
                    "Ты уже записан на игру! 🎮", 
                    cancellationToken: token);
                return;
            }

            userStates[userId] = new RegistrationState { Step = 1 };
            await client.SendTextMessageAsync(chatId, 
                "Отлично! Давай запишем тебя на игру.\n" +
                "Введи свою учебную группу (например, 'ИТ-21'):", 
                cancellationToken: token);
        }

        private static async Task HandleRegistrationStep(ITelegramBotClient client, long chatId, long userId, string text, CancellationToken token)
        {
            var state = userStates[userId];

            switch (state.Step)
            {
                case 1: // Группа
                    state.GroupNumber = text;
                    state.Step = 2;
                    await client.SendTextMessageAsync(chatId, 
                        "Хорошо! Теперь введи свое полное ФИО (например, 'Иванов Иван Иванович'):", 
                        cancellationToken: token);
                    break;

                case 2: // ФИО
                    state.FullName = text;
                    
                    // Сохраняем игрока в базу
                    var player = new Player
                    {
                        TelegramUserId = userId,
                        FirstName = state.FirstName,
                        LastName = state.LastName,
                        UserName = state.UserName,
                        GroupNumber = state.GroupNumber,
                        FullName = state.FullName,
                        RegistrationDate = DateTime.Now
                    };

                    if (dbService.RegisterPlayer(player))
                    {
                        await client.SendTextMessageAsync(chatId, 
                            "🎉 Поздравляю! Ты успешно записался на игру в мафию!\n" +
                            $"Группа: {state.GroupNumber}\n" +
                            $"ФИО: {state.FullName}\n\n" +
                            "Жди уведомления о начале игры!", 
                            cancellationToken: token);
                    }
                    else
                    {
                        await client.SendTextMessageAsync(chatId, 
                            "❌ Произошла ошибка при регистрации. Попробуй еще раз.", 
                            cancellationToken: token);
                    }

                    // Удаляем состояние
                    userStates.Remove(userId);
                    break;
            }
        }

        private static async Task HandlePlayerList(ITelegramBotClient client, long chatId, CancellationToken token)
        {
            var players = dbService.GetAllPlayers();

            if (players.Count == 0)
            {
                await client.SendTextMessageAsync(chatId, 
                    "Пока никто не записался на игру. Будь первым! 🎯", 
                    cancellationToken: token);
                return;
            }

            string message = "📋 Список участников мафии:\n\n";
            foreach (var player in players)
            {
                message += $"👤 {player.FullName}\n";
                message += $"🏫 Группа: {player.GroupNumber}\n";
                message += $"⏰ Записался: {player.RegistrationDate:dd.MM.yyyy HH:mm}\n";
                message += "────────────────────\n";
            }

            message += $"\nВсего участников: {players.Count}";

            await client.SendTextMessageAsync(chatId, message, cancellationToken: token);
        }

        private static async Task HandleUnregister(ITelegramBotClient client, long chatId, long userId, string userName, CancellationToken token)
        {
            if (dbService.UnregisterPlayer(userId))
            {
                await client.SendTextMessageAsync(chatId, 
                    "Твоя запись на игру отменена. Жаль, что ты не сможешь присоединиться! 😢", 
                    cancellationToken: token);
            }
            else
            {
                await client.SendTextMessageAsync(chatId, 
                    "Ты не был записан на игру.", 
                    cancellationToken: token);
            }
        }

        private static Task HandlePollingErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }

    public class RegistrationState
    {
        public int Step { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserName { get; set; }
        public string GroupNumber { get; set; }
        public string FullName { get; set; }
    }
}