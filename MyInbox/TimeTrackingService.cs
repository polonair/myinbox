
namespace MyInbox
{
    public class TimeTransaction
    {
        private DateTime m_stop = DateTime.MinValue;

        public DateTime Start { get; set; }
        public DateTime Stop
        {
            get => m_stop;
            set
            {
                m_stop = value;
                File.WriteAllLines(FileName, new string[]
                {
                    $"---",
                    $"start: {Start:s}",
                    $"stop: {Stop:s}",
                    $"task: \"[[{Task}]]\"",
                    $"---"
                });
            }
        }
        public string Task { get; set; }
        public TimeSpan Duration => (Start > Stop) ? (DateTime.Now - Start) : (Stop - Start);
        public string FileName { get; private set; }
        public string TaskPath 
        {
            get
            {
                foreach(var file in Directory.EnumerateFiles("g:/My Drive/sync/MyInbox/", $"{Task}.md", SearchOption.AllDirectories))
                {
                    return Path.GetRelativePath("g:/My Drive/sync/MyInbox/", Path.GetFullPath(file));
                }
                throw new FileNotFoundException();
            }
        }

        internal static bool IsTracking(string file)
        {
            var content = File.ReadAllLines(file);
            foreach (var line in content)
            {
                if (line.Trim().StartsWith("stop:"))
                {
                    if (DateTime.TryParse(line.Substring(5), out DateTime stop)) return false;
                    else return true;
                }
            }
            return true;
        }

        internal static TimeTransaction FromFile(string file)
        {
            TimeTransaction transaction = new TimeTransaction();
            transaction.FileName = file;
            var content = File.ReadAllLines(file);
            foreach (var line in content)
            {
                if (line.Trim().StartsWith("start:") && DateTime.TryParse(line.Substring(6), out DateTime start))
                    transaction.Start = start;
                if (line.Trim().StartsWith("stop:") && DateTime.TryParse(line.Substring(5), out DateTime stop))
                    transaction.Stop = stop;
                if (line.Trim().StartsWith("task:"))
                    transaction.Task = line.Substring(5).Trim().Trim('\"').Trim('[', ']');
            }
            return transaction;
        }

        internal static void CreateWithFile(string path)
        {
            new TimeTransaction()
            {
                FileName = path,
                Start = DateTime.Now,
                Task = ""
            }.Flush();
        }

        private void Flush()
        {
            File.WriteAllLines(FileName, new string[]
            {
                    $"---",
                    $"start: {Start:s}",
                    $"stop: {((Stop<Start)?"":Stop.ToString("s"))}",
                    $"task: \"[[{Task}]]\"",
                    $"---"
            });
        }
    }
    public class TimeTrackingService
    {
        public bool IsTracking 
        { 
            get
            {
                string path = $"g:/My Drive/sync/MyInbox/ttx/";
                foreach (var file in Directory.EnumerateFiles(path, "*.md"))
                {
                    if (TimeTransaction.IsTracking(file)) return true;
                }
                return false;
            } 
        }

        public TimeTransaction CurrentTimeTransaction 
        {
            get
            {
                string path = $"g:/My Drive/sync/MyInbox/ttx/";
                foreach (var file in Directory.EnumerateFiles(path, "*.md"))
                {
                    if (TimeTransaction.IsTracking(file))
                    {
                        return TimeTransaction.FromFile(file);
                    }
                }
                return null;
            } 
        }
        public void Start()
        {
            string path = $"g:/My Drive/sync/MyInbox/ttx/ttx-{DateTime.Now:yyyyMMddhhmmss}.md";
            TimeTransaction.CreateWithFile(path);
        }
        public async Task StartAsync(CancellationToken cancelationToken)
        {
            while (!cancelationToken.IsCancellationRequested)
            {
                Thread.Sleep(1000);
            }
        }
    }
}