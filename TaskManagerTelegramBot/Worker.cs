using TaskManagerTelegramBot.Classes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaskManagerTelegramBot
{
    public class Worker : BackgroundService
    {
        readonly string Token = "8521816007:AAGjayfKaYDf-vE0h4r8TE8aZBL_DVN57iU";
        TelegramBotClient TelegramBotClient;
        List<Users> Users = new List<Users>();
        Timer Timer;
        List<string> Messages = new List<string>()
        {
            "Здравствуйте! 👋\n" +
            "Рады приветствовать вас в Telegram-боте «Напоминалка»! 😊\n" +
            "Наш бот создан для того, чтобы напоминать вам о важных событиях и мероприятиях. " +
            "С ним вы точно не пропустите ничего важного! 💬\n" +
            "Не забудьте добавить бота в список своих контактов и настроить уведомления. " +
            "Тогда вы всегда будете в курсе событий! 😊",

            "Укажите дату и время напоминания в следующем формате:\n" +
            "<i><b>12:51 26.01.2025</b>\n" +
            "Напомни о том, что я хотел сходить в магазин.</i>",

            "Кажется, что-то не получилось.\n" +
            "Укажите дату и время напоминания в следующем формате:\n" +
            "<i><b>12:51 26.01.2025</b>\n" +
            "Напомни о том, что я хотел сходить в магазин.</i>",

            "Задачи пользователя не найдены.",

            "Событие удалено.",

            "Все события удалены."
        };

        public bool CheckFormatDateTime(string value, out DateTime time)
        {
            return DateTime.TryParse(value, out time);
        }

        private static ReplyKeyboardMarkup GetButtons()
        {
            List<KeyboardButton> keyboardButtons = new List<KeyboardButton>();
            keyboardButtons.Add(new KeyboardButton("Удалить все задачи"));
            keyboardButtons.Add(new KeyboardButton("Список задач"));
            keyboardButtons.Add(new KeyboardButton("Создать задачу"));

            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>>
                {
                    keyboardButtons
                }
            };
        }

        public static InlineKeyboardMarkup DeleteEvent(string Message)
        {
            List<InlineKeyboardButton> inlineKeyboards = new List<InlineKeyboardButton>();
            inlineKeyboards.Add(new InlineKeyboardButton("Удалить", Message));
            return new InlineKeyboardMarkup(inlineKeyboards);
        }

        public async Task SendMessage(long chatId, int typeMessage)
        {
            if (typeMessage != 3)
            {
                await TelegramBotClient.SendMessage(
                    chatId: chatId,
                    text: Messages[typeMessage],
                    parseMode: ParseMode.Html,
                    replyMarkup: GetButtons()
                );
            }
            else if (typeMessage == 3)
            {
                await TelegramBotClient.SendMessage(
                    chatId: chatId,
                    text: $"Указанное вами время и дата не могут быть установлены, " +
                          $"потому что сейчас уже : {DateTime.Now.ToString("HH.mm dd.MM.yyyy")}"
                );
            }
        }

        public async Task Command(long chatId, string command)
        {
            if (command.ToLower() == "/start") await SendMessage(chatId, 0);
            else if (command.ToLower() == "/create_task") await SendMessage(chatId, 1);
            else if (command.ToLower() == "/list_tasks")
            {
                Users User = Users.Find(x => x.IdUser == chatId);
                if (User == null) await SendMessage(chatId, 4);
                else if (User.Events.Count == 0) await SendMessage(chatId, 4);
                else
                {
                    foreach (Events Event in User.Events)
                    {
                        await TelegramBotClient.SendMessage(
                            chatId: chatId,
                            text: $"Уведомить пользователя: {Event.Time.ToString("HH:mm dd.MM.yyyy")}" +
                                  $"\nСообщение: {Event.Message}",
                            replyMarkup: DeleteEvent(Event.Message)
                        );
                    }
                }
            }
        }

        private async Task GetMessages(Message message)
        {
            if (message == null || message.Text == null) return;

            Console.WriteLine($"Получено сообщение: {message.Text} от пользователя: {message.Chat.Username}");
            string MessageUser = message.Text;

            if (message.Text.Contains("/"))
            {
                await Command(message.Chat.Id, message.Text);
            }
            else if (message.Text.Equals("Удалить все задачи"))
            {
                Users User = Users.Find(x => x.IdUser == message.Chat.Id);
                if (User == null)
                {
                    await SendMessage(message.Chat.Id, 4);
                }
                else if (User.Events.Count == 0)
                {
                    await SendMessage(User.IdUser, 4);
                }
                else
                {
                    User.Events = new List<Events>();
                    await SendMessage(User.IdUser, 6);
                }
            }
            else
            {
                Users User = Users.Find(x => x.IdUser == message.Chat.Id);
                if (User == null)
                {
                    User = new Users(message.Chat.Id);
                    Users.Add(User);
                }

                string[] Info = message.Text.Split('\n');
                if (Info.Length < 2)
                {
                    await SendMessage(message.Chat.Id, 2);
                    return;
                }

                DateTime Time;
                if (!CheckFormatDateTime(Info[0], out Time))
                {
                    await SendMessage(message.Chat.Id, 2);
                    return;
                }

                if (Time < DateTime.Now)
                {
                    await SendMessage(message.Chat.Id, 3);
                    return;
                }

                User.Events.Add(new Events(
                    Time,
                    message.Text.Replace(Time.ToString("HH:mm dd.MM.yyyy") + "\n", "")
                ));
            }
        }

        public async Task Tick(object obj)
        {
            string TimeNow = DateTime.Now.ToString("HH:mm dd.MM.yyyy");
            foreach (Users User in Users)
            {
                for (int i = User.Events.Count - 1; i >= 0; i--)
                {
                    if (User.Events[i].Time.ToString("HH:mm dd.MM.yyyy") != TimeNow) continue;

                    await TelegramBotClient.SendMessage(
                        chatId: User.IdUser,
                        text: "Напоминание: " + User.Events[i].Message
                    );

                    User.Events.RemoveAt(i);
                }
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is { } message)
            {
                await GetMessages(message);
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка Telegram Bot: {exception.Message}");
            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TelegramBotClient = new TelegramBotClient(Token);

            Console.WriteLine("Бот успешно запущен!");

            TelegramBotClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: null,
                cancellationToken: stoppingToken
            );

            TimerCallback TimerCallback = new TimerCallback(async (obj) => await Tick(obj));
            Timer = new Timer(TimerCallback, null, 0, 60 * 1000);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}