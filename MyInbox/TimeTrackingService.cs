
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace MyInbox
{
    public class TimeTransaction
    {
        public DateTime Start { get; set; }
        public DateTime Stop        {            get;            set;        }
        public string Task { get; set; }
        public TimeSpan Duration => (Start > Stop) ? (DateTime.Now - Start) : (Stop - Start);
        public string FileName { get; private set; }
        public string TaskPath 
        {
            get
            {
                foreach(var file in Directory.EnumerateFiles("g:/Мой диск/sync/MyInbox/", $"{Task}.md", SearchOption.AllDirectories))
                {
                    return Path.GetRelativePath("g:/Мой диск/sync/MyInbox/", Path.GetFullPath(file));
                }
                throw new FileNotFoundException();
            }
        }

        public string Note { get; private set; }
        public List<string> Tags { get; private set; } = null;

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
            int fmLimit = 2;
            bool inTags = false;
            foreach (var line in content)
            {
                if (line.Trim().StartsWith("---"))
                {
                    fmLimit--;
                    continue;
                }
                if (fmLimit > 0)
                {
                    if (line.Trim().StartsWith("start:") && DateTime.TryParse(line.Substring(6), out DateTime start))
                    {
                        inTags = false;
                        transaction.Start = start;
                    }
                    if (line.Trim().StartsWith("stop:") && DateTime.TryParse(line.Substring(5), out DateTime stop))
                    {
                        inTags = false;
                        transaction.Stop = stop;
                    }
                    if (line.Trim().StartsWith("task:"))
                    {
                        inTags = false;
                        transaction.Task = line.Substring(5).Trim().Trim('\"').Trim('[', ']');
                    }
                    if (line.Trim().StartsWith("tags:"))
                    {
                        inTags = true;
                        transaction.Tags = new List<string>();
                    }
                    if (line.Trim().StartsWith("- ") && inTags)
                    {
                        transaction.Tags.Add(line.TrimStart(' ', '-').Trim().Trim('\"').Trim('#'));
                    }
                }
                else 
                {
                    transaction.Note += (transaction.Note + "\n" + line).Trim();
                }
            }
            return transaction;
        }

        internal static void CreateWithFile(string path, string id = null)
        {
            new TimeTransaction()
            {
                FileName = path,
                Start = DateTime.Now,
                Task = string.IsNullOrEmpty(id) ? "" : id,
            }.Flush();
        }

        public void Flush()
        {

            var lst = new List<string>
            {
                    $"---",
                    $"start: {Start:s}",
                    $"stop: {((Stop<Start)?"":Stop.ToString("s"))}",
                    $"task: \"[[{Task}]]\"",
                    $"tags: ",
            };
            if (Tags != null) foreach (var item in Tags) lst.Add($"  - {item}");
            lst.Add("---");
            if (!string.IsNullOrEmpty(this.Note)) lst.AddRange(this.Note.Split('\n'));
            File.WriteAllLines(FileName, lst);
        }

        internal void StopNow()
        {
            this.Stop = DateTime.Now;
            Flush();
        }
    }
    public class TimeTrackingService
    {
        public bool IsTracking 
        { 
            get
            {
                string path = $"g:/Мой диск/sync/MyInbox/ttx/";
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
                string path = $"g:/Мой диск/sync/MyInbox/ttx/";
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
            string path = $"g:/Мой диск/sync/MyInbox/ttx/ttx-{DateTime.Now:yyyyMMddHHmmss}.md";
            TimeTransaction.CreateWithFile(path);
        }
        public async Task StartAsync(CancellationToken cancelationToken)
        {
            while (!cancelationToken.IsCancellationRequested)
            {
                Thread.Sleep(1000);
            }
        }

        internal void Start(string id)
        {
            string path = $"g:/Мой диск/sync/MyInbox/ttx/ttx-{DateTime.Now:yyyyMMddHHmmss}.md";
            TimeTransaction.CreateWithFile(path, id);
            UpdateTimeSheets();
        }

        public void UpdateTimeSheets()
        {

            string path = $"g:/Мой диск/sync/MyInbox/";
            foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.AllDirectories))
            {
                if (File.ReadAllText(file).Contains("%%:TIMESHEET("))
                {
                    UpdateTimeSheet(file);
                }
            }
        }

        private void UpdateTimeSheet(string file)
        {
            var content = File.ReadAllText(file);
            var ms = Regex.Matches(content, "%%:TIMESHEET\\('([^']+)',\\s*'([^']*)',\\s*'([^']+)',\\s*'([^']+)'\\):%%.*?%%:END:%%", RegexOptions.Singleline);
            foreach (Match m in ms)
            {
                if (m.Success)
                {
                    var type = m.Groups[1].Value;
                    var tag = m.Groups[2].Value;
                    var from = m.Groups[3].Value;
                    var to = m.Groups[4].Value;
                    var newContent = CreateTimeSheet(type, tag, from, to);
                    if (!string.IsNullOrEmpty(newContent))
                    {
                        var spl = m.Value.Split("%%", StringSplitOptions.RemoveEmptyEntries);
                        var head = $"%%{spl[0]}%%\n";
                        var end = $"%%:END:%%";
                        content = content.Replace(m.Value, $"{head}\n{newContent}\n{end}");
                    }
                }
            }
            File.WriteAllText(file, content);
        }

        private string CreateTimeSheet(string type, string tag, string from, string to)
        {
            switch (type)
            {
                case "PLAINLIST":

                    if (DateTime.TryParse(from, out var f) && DateTime.TryParse(to, out var t))
                    {
                        return CreateTSPlainList(tag, f, t);
                    }
                    break;
                default: return null;
            }
            return null;
        }

        private string CreateTSPlainList(string tag, DateTime from, DateTime to)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("task", typeof(string));
            dt.Columns.Add("note", typeof(string));
            dt.Columns.Add("duaration", typeof(TimeSpan));
            dt.Columns.Add("started", typeof(DateTime));
            string path = $"g:/Мой диск/sync/MyInbox/ttx/";
            TimeSpan summ = TimeSpan.Zero;
            
            foreach (var file in Directory.EnumerateFiles(path, "*.md"))
            {
                var ttx = TimeTransaction.FromFile(file);
                if (ttx.Tags.Contains(tag) && from < ttx.Start && to > ttx.Start)
                {
                    dt.Rows.Add($"[[{ttx.Task}]]", $"![[{Path.GetFileName(ttx.FileName)}]]", ttx.Duration, ttx.Start);
                    summ += ttx.Duration;
                }
            }
            dt.Rows.Add($"Итог:", $"", summ, DateTime.Now);

            return dt.ToMarkdown();
        }
    }
}