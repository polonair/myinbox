using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MyInbox
{
    public class TelegramService 
    {
        const string BOT_API_KEY = NONE;
        const string API_URL = $"https://api.telegram.org/bot{BOT_API_KEY}/";

        private readonly HttpClient httpClient = new HttpClient();
        private readonly TimeTrackingService _tracker;

        public TelegramService(TimeTrackingService tracker) 
        {
            _tracker = tracker;
        }

        public async Task StartAsync(CancellationToken cancelationToken)
        {
            long lastUpdateId = 0;

            while (!cancelationToken.IsCancellationRequested)
            {
                try
                {
                    var updates = await GetUpdatesAsync(lastUpdateId, cancelationToken);
                    if (updates == null) continue;
                    foreach (var update in updates)
                    {
                        lastUpdateId = update.update_id + 1;
                        await HandleUpdateAsync(update, cancelationToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    await Task.Delay(5000, cancelationToken); // Подождать перед новой попыткой
                }
            }
        }

        private async Task HandleUpdateAsync(TelegramUpdate update, CancellationToken ct)
        {
            if (update == null) return;

            if (update.message.text.StartsWith("/start"))
            {
                var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                {
                    chat_id = update.message.chat.id,
                    text = "Привет от бота!",
                });
                response.EnsureSuccessStatusCode();
            }
            else if (update.message.text.StartsWith("/inbox"))
            {
                var rawFileName = $"{DateTime.Now:yyyyMMddhhmmss}.md";
                var realtiveFileName = $"inbox/{rawFileName}";
                var absoluteFileName = $"g:/My Drive/sync/MyInbox/{realtiveFileName}";
                File.WriteAllText(absoluteFileName, update.message.text.Substring(6));

                var append = $"* ![[{realtiveFileName}]]\n";

                rawFileName = $"INBOX.md";
                realtiveFileName = $"{rawFileName}";
                absoluteFileName = $"g:/My Drive/sync/MyInbox/{realtiveFileName}";
                File.AppendAllText(absoluteFileName, append);

                var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                {
                    chat_id = update.message.chat.id,
                    text = "Заметка сохранена в INBOX",
                });
                response.EnsureSuccessStatusCode();
            }
            else if (update.message.text.StartsWith("/begin"))
            {
                _tracker.Start();
                var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                {
                    chat_id = update.message.chat.id,
                    text = "Отсчет пошел",
                });
                response.EnsureSuccessStatusCode();
            }
            else if (update.message.text.StartsWith("/end"))
            {
                if (_tracker.IsTracking)
                {
                    var record = _tracker.CurrentTimeTransaction;
                    record.Stop = DateTime.Now;

                    var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                    {
                        chat_id = update.message.chat.id,
                        text = 
                            $"Таймер запущен в {record.Start}\n" +
                            $"Остановлен в {record.Stop}\n" +
                            $"Задача: {record.Task}\n" +
                            $"Длительность {record.Duration}"
                    });
                    response.EnsureSuccessStatusCode();
                }
            }
            else if (update.message.text.StartsWith("/status"))
            {
                if (!_tracker.IsTracking)
                {
                    var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                    {
                        chat_id = update.message.chat.id,
                        text = "Таймер остановлен",
                    });
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    var record = _tracker.CurrentTimeTransaction;
                    var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                    {
                        chat_id = update.message.chat.id,
                        text = 
                            $"Таймер запущен в {record.Start}\n" +
                            $"Задача: {record.Task}\n" +
                            $"Длительность {record.Duration}"
                    });
                    response.EnsureSuccessStatusCode();
                }
            }
            else
            {
                var rawFileName = $"{DateTime.Now:yyyyMMddhhmmss}.md";
                var realtiveFileName = $"inbox/{rawFileName}";
                var absoluteFileName = $"g:/My Drive/sync/MyInbox/{realtiveFileName}";
                File.WriteAllText(absoluteFileName, update.message.text);

                var append = $"* ![[{realtiveFileName}]]\n";

                if (!_tracker.IsTracking)
                {

                    rawFileName = $"INBOX.md";
                    realtiveFileName = $"{rawFileName}";
                    absoluteFileName = $"g:/My Drive/sync/MyInbox/{realtiveFileName}";
                    File.AppendAllText(absoluteFileName, append);

                    var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                    {
                        chat_id = update.message.chat.id,
                        text = "Заметка сохранена в INBOX",
                    });
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    var record = _tracker.CurrentTimeTransaction;

                    var rel = Path.GetDirectoryName(record.TaskPath);

                    rawFileName = $"ACTIVITY.md";
                    realtiveFileName = $"{rel}/{record.Task}/{rawFileName}";
                    absoluteFileName = $"g:/My Drive/sync/MyInbox/{realtiveFileName}";
                    absoluteFileName.EnsureDirectoryExists();
                    File.AppendAllText(absoluteFileName, append);

                    var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                    {
                        chat_id = update.message.chat.id,
                        text = $"Заметка сохранена в {record.Task}/ACTIVITY",
                    });
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        private async Task<List<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken ct)
        {
            var response = await httpClient.GetAsync($"{API_URL}getUpdates?offset={offset}&timeout=60", ct);
            var content = await response?.Content.ReadAsStringAsync();
            var update = JsonSerializer.Deserialize<TelegramResponse>(content);
            return update.result;
        }
    }
    static class StringExtensions
    {
        public static void EnsureDirectoryExists(this string path)
        {
            string dir = Path.GetDirectoryName (path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
    }
}