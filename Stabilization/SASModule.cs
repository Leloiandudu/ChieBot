using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.Stabilization
{
    /// <summary>
    /// Status article stabilisation
    /// </summary>
    class SASModule : Modules.IModule
    {
        public void Execute(MediaWiki wiki, string[] commandLine)
        {
            Stabilize(wiki, "Википедия:Кандидаты в хорошие статьи/Журнал избраний", new LastDateChecked("sas-lastrev"));
        }

        private void Stabilize(MediaWiki wiki, string logTitle, LastDateChecked last)
        {
            var lines = ParseLog(wiki.GetPage(logTitle));

            var lastDate = last.Get() ?? DateTimeOffset.MinValue;
            // checking for logDate >= lastDate because log date has minute resolution
            foreach(var page in lines.Where(l => l.Date >= lastDate).SelectMany(l => l.Titles).Select(MediaWiki.UnscapeTitle).Distinct())
            {
                StabilizeArticle(wiki, page);
            }

            var nextDate = lines.Max(l => l.Date);

            // If the last log entry was long enough ago, we can safely move the pointer one 
            // minute forward. This way we won't have to check the same entry next time.
            if (nextDate.AddMinutes(2) < DateTimeOffset.UtcNow)
                nextDate = nextDate.AddMinutes(1);

            last.Set(nextDate);
        }

        private static readonly Regex StatusTemplateRegex = new Regex(@"\{\{Хорошая статья[|}]", RegexOptions.IgnoreCase);
        private void StabilizeArticle(MediaWiki wiki, string page)
        {
            var text = wiki.GetPage(page);
            if (text != null && StatusTemplateRegex.IsMatch(text))
            {
                wiki.Stabilize(page, "Автоматическая стабилизация хорошей статьи.", null);
            }
        }

        private static readonly Regex LogLineRegex = new Regex(@"^\*(.*)(\d\d:\d\d, \d+ \w+ \d{4}) \(UTC\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex LinkRegex = new Regex(@"\[\[(?!(User|У|Участник|User talk|ОУ|Обсуждение участника):)(?<link>[^|#\]]+)(#[^|\]]*)?(\|[^\]]*)?\]\]", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        private static LogLine[] ParseLog(string text)
        {
            return (
                from Match m in LogLineRegex.Matches(text)
                select new LogLine
                {
                    Date = Utils.ParseDate(m.Groups[2].Value),
                    Titles = (
                        from Match mm in LinkRegex.Matches(m.Groups[1].Value)
                        select mm.Groups["link"].Value
                    ).ToArray(),
                }
            ).ToArray();
        }

        class LogLine
        {
            public DateTimeOffset Date { get; set; }
            public string[] Titles { get; set; }
        }
    }
}
