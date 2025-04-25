
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
                foreach(var file in Directory.EnumerateFiles("g:/My Drive/sync/MyInbox/", $"{Task}.md", SearchOption.AllDirectories))
                {
                    return Path.GetRelativePath("g:/My Drive/sync/MyInbox/", Path.GetFullPath(file));
                }
                throw new FileNotFoundException();
            }
        }

        public string Note { get; private set; }

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
                        transaction.Start = start;
                    if (line.Trim().StartsWith("stop:") && DateTime.TryParse(line.Substring(5), out DateTime stop))
                        transaction.Stop = stop;
                    if (line.Trim().StartsWith("task:"))
                        transaction.Task = line.Substring(5).Trim().Trim('\"').Trim('[', ']');
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

        private void Flush()
        {

            var lst = new List<string>
            {
                    $"---",
                    $"start: {Start:s}",
                    $"stop: {((Stop<Start)?"":Stop.ToString("s"))}",
                    $"task: \"[[{Task}]]\"",
                    $"---"
            };
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
            string path = $"g:/My Drive/sync/MyInbox/ttx/ttx-{DateTime.Now:yyyyMMddHHmmss}.md";
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
            string path = $"g:/My Drive/sync/MyInbox/ttx/ttx-{DateTime.Now:yyyyMMddHHmmss}.md";
            TimeTransaction.CreateWithFile(path, id);
            UpdateTimeSheets();
        }

        public void UpdateTimeSheets()
        {

            string path = $"g:/My Drive/sync/MyInbox/";
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
            var ms = Regex.Matches(content, "%%:TIMESHEET\\('([^']+)',\\s*'([^']*)',\\s*'([^']+)',\\s*'([^']+)'\\):%%.*%%:END:%%", RegexOptions.Singleline);
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
            string path = $"g:/My Drive/sync/MyInbox/ttx/";
            
            foreach (var file in Directory.EnumerateFiles(path, "*.md"))
            {
                var ttx = TimeTransaction.FromFile(file);
                if (from < ttx.Start && to > ttx.Start)
                    dt.Rows.Add($"[[{ttx.Task}]]", ttx.Note, ttx.Duration, ttx.Start);
            }

            return dt.ToMarkdown();
        }
    }
}