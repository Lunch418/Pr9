using TaskManagerTelegramBot.Classes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;

namespace TaskManagerTelegramBot
{
    public class Worker : BackgroundService
    {
        readonly string Token = "8521816007:AAGjayfKaYDf-vE0h4r8TE8aZBL_DVN57iU";
        TelegramBotClient? TelegramBotClient;
        Timer? Timer;

        private readonly ApplicationDbContext _dbContext;

        List<string> Messages = new List<string>()
        {
            "Здравствуйте! 👋\n" +
            "Рады приветствовать вас в Telegram-боте «Напоминалка»! 😊\n" +
            "Наш бот создан для того, чтобы напоминать вам о важных событиях и мероприятиях. " +
            "С ним вы точно не пропустите ничего важного! 💬\n" +
            "Не забудьте добавить бота в список своих контактов и настроить уведомления. " +
            "Тогда вы всегда будете в курсе событий! 😊",

            "Укажите дату и время напоминания в следующем формате:\n" +
            "<i><b>12:51 01.01.2025</b>\n" +
            "Напомни о том, что я хотел сходить в магазин.</i>",

            "Кажется, что-то не получилось.\n" +
            "Укажите дату и время напоминания в следующем формате:\n" +
            "<i><b>12:51 26.01.2025</b>\n" +
            "Напомни о том, что я хотел сходить в магазин.</i>",

            "Указанное вами время и дата не могут быть установлены, потому что сейчас уже : {0}",

            "Задачи пользователя не найдены.",

            "Событие удалено.",

            "Все события удалены.",

            "Укажите дни недели и время для повторяющегося напоминания в формате:\n" +
            "<i><b>вторник, среда 21:00</b>\n" +
            "Полить цветы</i>\n\n" +
            "Доступные дни недели: понедельник, вторник, среда, четверг, пятница, суббота, воскресенье\n" +
            "Можно использовать сокращения: пн, вт, ср, чт, пт, сб, вс"
        };

        public Worker()
        {
            _dbContext = new ApplicationDbContext();
        }

        public bool CheckFormatDateTime(string value, out DateTime time)
        {
            return DateTime.TryParse(value, out time);
        }

        private static ReplyKeyboardMarkup GetButtons()
        {
            List<KeyboardButton> row1 = new List<KeyboardButton>
            {
                new KeyboardButton("Создать задачу"),
                new KeyboardButton("Создать повторяющуюся")
            };

            List<KeyboardButton> row2 = new List<KeyboardButton>
            {
                new KeyboardButton("Список задач"),
                new KeyboardButton("Список повторяющихся")
            };

            List<KeyboardButton> row3 = new List<KeyboardButton>
            {
                new KeyboardButton("Удалить все задачи")
            };

            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>> { row1, row2, row3 },
                ResizeKeyboard = true
            };
        }

        public static InlineKeyboardMarkup DeleteEvent(int eventId)
        {
            List<InlineKeyboardButton> inlineKeyboards = new List<InlineKeyboardButton>();
            inlineKeyboards.Add(new InlineKeyboardButton("Удалить", $"delete_{eventId}"));
            return new InlineKeyboardMarkup(inlineKeyboards);
        }

        public static InlineKeyboardMarkup DeleteRepeatEvent(int repeatEventId)
        {
            List<InlineKeyboardButton> inlineKeyboards = new List<InlineKeyboardButton>();
            inlineKeyboards.Add(new InlineKeyboardButton("Удалить", $"delete_repeat_{repeatEventId}"));
            return new InlineKeyboardMarkup(inlineKeyboards);
        }

        public async Task SendMessage(long chatId, int typeMessage, bool showButtons = true)
        {
            if (TelegramBotClient == null) return;

            try
            {
                string messageText;

                if (typeMessage == 3) 
                {
                    messageText = string.Format(Messages[typeMessage], DateTime.Now.ToString("HH:mm dd.MM.yyyy"));
                }
                else if (typeMessage >= 0 && typeMessage < Messages.Count)
                {
                    messageText = Messages[typeMessage];
                }
                else
                {
                    messageText = "Сообщение не найдено.";
                }

                var replyMarkup = showButtons ? GetButtons() : null;

                await TelegramBotClient.SendMessage(
                    chatId: chatId,
                    text: messageText,
                    parseMode: ParseMode.Html,
                    replyMarkup: replyMarkup
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки сообщения: {ex.Message}");
            }
        }

        public async Task Command(long chatId, string command, string? username = null)
        {
            if (TelegramBotClient == null) return;

            try
            {
                // Маппинг русских названий кнопок на команды
                string actualCommand = command.ToLower().Trim();

                if (actualCommand == "/start" || actualCommand == "старт")
                {
                    await SendMessage(chatId, 0);
                    await EnsureUserExists(chatId, username);
                }
                else if (actualCommand == "/create_task" || actualCommand == "создать задачу")
                {
                    await SendMessage(chatId, 1);
                }
                else if (actualCommand == "/create_repeat_task" || actualCommand == "создать повторяющуюся")
                {
                    // Используем индекс 7 для повторяющихся задач
                    await SendMessage(chatId, 7);
                }
                else if (actualCommand == "/list_tasks" || actualCommand == "список задач")
                {
                    var events = await _dbContext.Events
                        .Where(e => e.UserId == chatId && !e.IsCompleted && e.Time >= DateTime.Now)
                        .OrderBy(e => e.Time)
                        .ToListAsync();

                    if (!events.Any())
                    {
                        await SendMessage(chatId, 4);
                    }
                    else
                    {
                        foreach (var ev in events)
                        {
                            await TelegramBotClient.SendMessage(
                                chatId: chatId,
                                text: $"📅 {ev.Time.ToString("HH:mm dd.MM.yyyy")}\n" +
                                      $"📝 {ev.Message}",
                                replyMarkup: DeleteEvent(ev.Id)
                            );
                        }
                    }
                }
                else if (actualCommand == "/list_repeat_tasks" || actualCommand == "список повторяющихся")
                {
                    var repeatEvents = await _dbContext.RepeatEvents
                        .Where(re => re.UserId == chatId && re.IsActive)
                        .ToListAsync();

                    if (!repeatEvents.Any())
                    {
                        await TelegramBotClient.SendMessage(
                            chatId,
                            "У вас нет повторяющихся напоминаний.",
                            replyMarkup: GetButtons()
                        );
                    }
                    else
                    {
                        foreach (var repeatEvent in repeatEvents)
                        {
                            string daysStr = string.Join(", ", repeatEvent.Days.Select(d => GetRussianDayName(d)));
                            await TelegramBotClient.SendMessage(
                                chatId: chatId,
                                text: $"🔄 Повторяющееся напоминание:\n" +
                                      $"📅 Дни: {daysStr}\n" +
                                      $"⏰ Время: {repeatEvent.Time:hh\\:mm}\n" +
                                      $"📝 {repeatEvent.Message}",
                                replyMarkup: DeleteRepeatEvent(repeatEvent.Id)
                            );
                        }
                    }
                }
                else
                {
                    // Если неизвестная команда, показываем кнопки
                    await TelegramBotClient.SendMessage(
                        chatId: chatId,
                        text: "Пожалуйста, используйте кнопки ниже:",
                        replyMarkup: GetButtons()
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в Command: {ex.Message}");
                await TelegramBotClient.SendMessage(
                    chatId: chatId,
                    text: "Произошла ошибка. Попробуйте еще раз.",
                    replyMarkup: GetButtons()
                );
            }
        }

        private async Task EnsureUserExists(long userId, string? username)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    user = new Classes.User
                    {
                        Id = userId,
                        Username = username,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _dbContext.Users.AddAsync(user);
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания пользователя: {ex.Message}");
            }
        }

        private async Task GetMessages(Message message)
        {
            if (message == null || message.Text == null) return;

            Console.WriteLine($"Получено сообщение: {message.Text} от пользователя: {message.Chat.Username}");

            await EnsureUserExists(message.Chat.Id, message.Chat.Username);

            try
            {
                // Обрабатываем команды (слэш-команды и русские названия кнопок)
                if (message.Text.StartsWith("/") ||
                    message.Text == "Создать задачу" ||
                    message.Text == "Создать повторяющуюся" ||
                    message.Text == "Список задач" ||
                    message.Text == "Список повторяющихся" ||
                    message.Text == "Удалить все задачи")
                {
                    await Command(message.Chat.Id, message.Text, message.Chat.Username);
                }
                else if (message.Text.Equals("Удалить все задачи"))
                {
                    var userEvents = await _dbContext.Events
                        .Where(e => e.UserId == message.Chat.Id)
                        .ToListAsync();

                    var repeatEvents = await _dbContext.RepeatEvents
                        .Where(re => re.UserId == message.Chat.Id)
                        .ToListAsync();

                    int totalCount = userEvents.Count + repeatEvents.Count;

                    if (totalCount == 0)
                    {
                        await SendMessage(message.Chat.Id, 4);
                    }
                    else
                    {
                        if (userEvents.Any())
                        {
                            _dbContext.Events.RemoveRange(userEvents);
                        }

                        if (repeatEvents.Any())
                        {
                            _dbContext.RepeatEvents.RemoveRange(repeatEvents);
                        }

                        await _dbContext.SaveChangesAsync();

                        await TelegramBotClient.SendMessage(
                            message.Chat.Id,
                            $"✅ Удалено {totalCount} задач!",
                            replyMarkup: GetButtons()
                        );
                    }
                }
                else
                {
                    // Пытаемся сначала распарсить как повторяющуюся задачу
                    if (TryParseRepeatTask(message.Text, out List<DayOfWeek> days, out TimeSpan time, out string taskMessage))
                    {
                        var repeatEvent = new RepeatEvent(days, time, taskMessage)
                        {
                            UserId = message.Chat.Id,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _dbContext.RepeatEvents.AddAsync(repeatEvent);
                        await _dbContext.SaveChangesAsync();

                        string daysStr = string.Join(", ", days.Select(d => GetRussianDayName(d)));
                        await TelegramBotClient.SendMessage(
                            message.Chat.Id,
                            $"✅ Повторяющееся напоминание создано!\n" +
                            $"📅 Дни: {daysStr}\n" +
                            $"⏰ Время: {time:hh\\:mm}\n" +
                            $"📝 {taskMessage}",
                            replyMarkup: GetButtons()
                        );
                        return;
                    }

                    // Если не повторяющаяся задача, парсим как обычную
                    string[] Info = message.Text.Split('\n');
                    if (Info.Length < 2)
                    {
                        await SendMessage(message.Chat.Id, 2);
                        return;
                    }

                    DateTime Time;
                    if (!CheckFormatDateTime(Info[0].Trim(), out Time))
                    {
                        await SendMessage(message.Chat.Id, 2);
                        return;
                    }

                    if (Time < DateTime.Now)
                    {
                        await SendMessage(message.Chat.Id, 3);
                        return;
                    }

                    var ev = new Event(Time, Info[1].Trim())
                    {
                        UserId = message.Chat.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _dbContext.Events.AddAsync(ev);
                    await _dbContext.SaveChangesAsync();

                    await TelegramBotClient.SendMessage(
                        message.Chat.Id,
                        $"✅ Напоминание создано!\n" +
                        $"📅 {Time.ToString("HH:mm dd.MM.yyyy")}\n" +
                        $"📝 {Info[1].Trim()}",
                        replyMarkup: GetButtons()
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки сообщения: {ex.Message}");
                await TelegramBotClient.SendMessage(
                    message.Chat.Id,
                    "Произошла ошибка. Пожалуйста, проверьте формат ввода.",
                    replyMarkup: GetButtons()
                );
            }
        }

        private bool TryParseRepeatTask(string text, out List<DayOfWeek> days, out TimeSpan time, out string taskMessage)
        {
            days = new List<DayOfWeek>();
            time = TimeSpan.Zero;
            taskMessage = "";

            try
            {
                string[] lines = text.Split('\n');
                if (lines.Length < 2) return false;

                string header = lines[0].Trim();
                taskMessage = lines[1].Trim();

                if (string.IsNullOrEmpty(taskMessage)) return false;

                // Извлекаем время (последнее слово в формате HH:mm)
                var timeMatch = System.Text.RegularExpressions.Regex.Match(header, @"(\d{1,2}):(\d{1,2})");
                if (!timeMatch.Success) return false;

                if (!int.TryParse(timeMatch.Groups[1].Value, out int hours) ||
                    !int.TryParse(timeMatch.Groups[2].Value, out int minutes))
                    return false;

                if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
                    return false;

                time = new TimeSpan(hours, minutes, 0);

                // Убираем время из строки для парсинга дней
                string daysPart = header.Replace(timeMatch.Value, "").Trim().ToLower();

                // Если после удаления времени ничего не осталось
                if (string.IsNullOrWhiteSpace(daysPart)) return false;

                // Список русских названий дней
                var russianDays = new Dictionary<string, DayOfWeek>
                {
                    {"понедельник", DayOfWeek.Monday}, {"пон", DayOfWeek.Monday}, {"пн", DayOfWeek.Monday},
                    {"вторник", DayOfWeek.Tuesday}, {"вто", DayOfWeek.Tuesday}, {"вт", DayOfWeek.Tuesday},
                    {"среда", DayOfWeek.Wednesday}, {"сре", DayOfWeek.Wednesday}, {"ср", DayOfWeek.Wednesday},
                    {"четверг", DayOfWeek.Thursday}, {"чет", DayOfWeek.Thursday}, {"чт", DayOfWeek.Thursday},
                    {"пятница", DayOfWeek.Friday}, {"пят", DayOfWeek.Friday}, {"пт", DayOfWeek.Friday},
                    {"суббота", DayOfWeek.Saturday}, {"суб", DayOfWeek.Saturday}, {"сб", DayOfWeek.Saturday},
                    {"воскресенье", DayOfWeek.Sunday}, {"вос", DayOfWeek.Sunday}, {"вс", DayOfWeek.Sunday}
                };

                // Убираем лишние пробелы и разделяем дни
                daysPart = System.Text.RegularExpressions.Regex.Replace(daysPart, @"\s+", " ");
                var dayParts = daysPart.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var dayPart in dayParts)
                {
                    var normalizedDay = dayPart.Trim();
                    if (russianDays.TryGetValue(normalizedDay, out DayOfWeek day))
                    {
                        if (!days.Contains(day))
                            days.Add(day);
                    }
                }

                return days.Any() && !string.IsNullOrEmpty(taskMessage);
            }
            catch
            {
                return false;
            }
        }

        private string GetRussianDayName(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "понедельник",
                DayOfWeek.Tuesday => "вторник",
                DayOfWeek.Wednesday => "среда",
                DayOfWeek.Thursday => "четверг",
                DayOfWeek.Friday => "пятница",
                DayOfWeek.Saturday => "суббота",
                DayOfWeek.Sunday => "воскресенье",
                _ => day.ToString()
            };
        }

        public async Task Tick(object? obj)
        {
            if (TelegramBotClient == null) return;

            try
            {
                var currentTime = DateTime.Now;

                // Проверяем обычные задачи - загружаем все и фильтруем в памяти
                var allEvents = await _dbContext.Events
                    .Where(e => !e.IsCompleted)
                    .Include(e => e.User)
                    .ToListAsync();

                var eventsToNotify = allEvents
                    .Where(e => e.Time.Year == currentTime.Year &&
                           e.Time.Month == currentTime.Month &&
                           e.Time.Day == currentTime.Day &&
                           e.Time.Hour == currentTime.Hour &&
                           e.Time.Minute == currentTime.Minute)
                    .ToList();

                foreach (var ev in eventsToNotify)
                {
                    if (ev.User != null)
                    {
                        await TelegramBotClient.SendMessage(
                            ev.User.Id,
                            $"🔔 Напоминание!\n📝 {ev.Message}",
                            replyMarkup: GetButtons()
                        );
                        // Удаляем выполненное напоминание из БД
                        _dbContext.Events.Remove(ev);
                    }
                }

                // Проверяем повторяющиеся задачи - загружаем все и фильтруем в памяти
                var allRepeatEvents = await _dbContext.RepeatEvents
                    .Where(re => re.IsActive)
                    .Include(re => re.User)
                    .ToListAsync();

                var repeatEventsToNotify = allRepeatEvents
                    .Where(re => re.Days.Contains(currentTime.DayOfWeek) &&
                           re.Time.Hours == currentTime.Hour &&
                           re.Time.Minutes == currentTime.Minute &&
                           (re.LastSent == null || re.LastSent.Value.Date < currentTime.Date))
                    .ToList();

                foreach (var repeatEvent in repeatEventsToNotify)
                {
                    if (repeatEvent.User != null)
                    {
                        await TelegramBotClient.SendMessage(
                            repeatEvent.User.Id,
                            $"🔁 Повторяющееся напоминание!\n📝 {repeatEvent.Message}",
                            replyMarkup: GetButtons()
                        );
                        repeatEvent.LastSent = currentTime;
                    }
                }

                if (eventsToNotify.Any() || repeatEventsToNotify.Any())
                {
                    await _dbContext.SaveChangesAsync();
                    Console.WriteLine($"Отправлено {eventsToNotify.Count} напоминаний и {repeatEventsToNotify.Count} повторяющихся напоминаний");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в Tick: {ex.Message}");
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type == UpdateType.Message && update.Message != null)
                {
                    await GetMessages(update.Message);
                }
                else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                {
                    var query = update.CallbackQuery;
                    var data = query.Data;

                    if (data.StartsWith("delete_"))
                    {
                        if (int.TryParse(data.Substring(7), out int eventId))
                        {
                            var ev = await _dbContext.Events.FindAsync(eventId);
                            if (ev != null && ev.UserId == query.Message.Chat.Id)
                            {
                                _dbContext.Events.Remove(ev);
                                await _dbContext.SaveChangesAsync();

                                await TelegramBotClient.AnswerCallbackQuery(query.Id, "Напоминание удалено!");

                                await TelegramBotClient.EditMessageText(
                                    query.Message.Chat.Id,
                                    query.Message.MessageId,
                                    "🗑 Напоминание удалено"
                                );
                            }
                        }
                    }
                    else if (data.StartsWith("delete_repeat_"))
                    {
                        if (int.TryParse(data.Substring(14), out int repeatEventId))
                        {
                            var repeatEvent = await _dbContext.RepeatEvents.FindAsync(repeatEventId);
                            if (repeatEvent != null && repeatEvent.UserId == query.Message.Chat.Id)
                            {
                                repeatEvent.IsActive = false;
                                await _dbContext.SaveChangesAsync();

                                await TelegramBotClient.AnswerCallbackQuery(query.Id, "Повторяющееся напоминание удалено!");

                                await TelegramBotClient.EditMessageText(
                                    query.Message.Chat.Id,
                                    query.Message.MessageId,
                                    "🗑 Повторяющееся напоминание удалено"
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки update: {ex.Message}");
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

            try
            {
                // Создаем базу данных, если не существует
                await _dbContext.Database.EnsureCreatedAsync();
                Console.WriteLine("Бот успешно запущен и подключен к БД!");

                TelegramBotClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandleErrorAsync,
                    receiverOptions: null,
                    cancellationToken: stoppingToken
                );

                TimerCallback TimerCallback = new TimerCallback(async (obj) => await Tick(obj));
                Timer = new Timer(TimerCallback, null, 0, 60 * 1000);

                Console.WriteLine("Таймер запущен. Бот готов к работе!");

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка запуска бота: {ex.Message}");
            }
        }
    }
}