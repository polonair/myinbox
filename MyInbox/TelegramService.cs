using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
namespace MyInbox
{
    class MyTask
    {
        public string Id { get; private set; }
        public string FileName { get; private set; }
        public string Type { get; private set; }
        public string Status { get; private set; }
        public string Title { get; private set; }

        internal static MyTask FromFile(string file)
        {
            MyTask result = new MyTask() { Id = Path.GetFileNameWithoutExtension(file), FileName = file };
            bool frontmatter = false;
            var content = File.ReadAllLines(file);

            foreach(var l in content)
            {
                var line = l.Trim();
                if (line == "---") frontmatter = !frontmatter;
                if (frontmatter) 
                {
                    if (line.StartsWith("type:")) result.Type = line.Substring(5).Trim();
                    else if (line.StartsWith("status:")) result.Status = line.Substring(7).Trim();
                }
                else 
                { 
                    if (line.StartsWith("# ")) result.Title = line.Substring(2).Trim();
                }
            }
            return result;
        }
    }
    class MyTaskController
    {
        internal static List<MyTask> GetTasks()
        {
            var result = new List<MyTask>();
            foreach (var file in Directory.EnumerateFiles("g:/My Drive/sync/MyInbox/", "*.md", SearchOption.AllDirectories))
            {
                var content = File.ReadAllLines(file);
                if (content.Length > 1 && content[0].Trim() == "---")
                {
                    string type = "";
                    for (int i =1; i< content.Length; i++)
                    {
                        if (content[i].StartsWith("type:"))
                        {
                            type = content[i].Substring(5).Trim();
                            break;
                        }
                    }
                    if (type.ToLower() == "task")
                    {
                        result.Add(MyTask.FromFile(file));
                    }
                }
            }
            return result;
        }
    }
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
                var rawFileName = $"{DateTime.Now:yyyyMMddHHmmss}.md";
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
            else if (update.message.text.StartsWith("/tasks"))
            {
                StringBuilder msg = new StringBuilder();
                List<MyTask> tasks = MyTaskController.GetTasks();

                foreach(var task in tasks)
                {
                    msg.Append($"* ({task.Status}) {task.Id}: {task.Title} /begin@{task.Id.Replace('-', '_')} /note@{task.Id.Replace('-', '_')}\n");
                }

                var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                {
                    chat_id = update.message.chat.id,
                    text = msg.ToString(),
                });
                response.EnsureSuccessStatusCode();
            }
            else if (update.message.text.StartsWith("/note@"))
            {
                List<MyTask> tasks = MyTaskController.GetTasks();

                foreach (var t in tasks)
                {
                    if (t.Id.Replace('-', '_') == update.message.text.Substring(6, update.message.text.IndexOf(' ')-6).Trim())
                    {
                        var rawFileName = $"{DateTime.Now:yyyyMMddHHmmss}.md";
                        var realtiveFileName = $"inbox/{rawFileName}";
                        var absoluteFileName = $"g:/My Drive/sync/MyInbox/{realtiveFileName}";
                        File.WriteAllText(absoluteFileName, update.message.text.Substring(update.message.text.IndexOf(' ')).Trim());

                        var append = $"* ![[{realtiveFileName}]]\n";

                        var path = Path.GetDirectoryName(t.FileName);
                        var p = Path.Combine(path, t.Id, "ACTIVITY.md");

                        p.EnsureDirectoryExists();
                        File.AppendAllText(p, append);

                        var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                        {
                            chat_id = update.message.chat.id,
                            text = $"Заметка сохранена в {t.Id}/ACTIVITY",
                        });
                        response.EnsureSuccessStatusCode();
                    }
                }
            }
            else if (update.message.text.StartsWith("/begin@"))
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

                List<MyTask> tasks = MyTaskController.GetTasks();

                foreach (var t in tasks)
                {
                    if (t.Id.Replace('-', '_') == update.message.text.Substring(7).Trim())
                    {
                        _tracker.Start(t.Id);
                        var response = await httpClient.PostAsJsonAsync($"{API_URL}sendMessage", new
                        {
                            chat_id = update.message.chat.id,
                            text = $"Отсчет пошел ({t.Id} - {t.Title})",
                        });
                        response.EnsureSuccessStatusCode();
                    }
                }
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
                var rawFileName = $"{DateTime.Now:yyyyMMddHHmmss}.md";
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
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
        public static string TgEscape(this string str)
        {
            //   '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'
            return str;
        }
    }
}