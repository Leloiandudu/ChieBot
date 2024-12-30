using System;
using System.Linq;

namespace ChieBot.Archiving
{
    class ArchiveModule : Modules.IModule
    {
        private const string EditSummary = "Автоматический аркайвинг";

        private readonly ArchiveRule[] _rules = new[]
        {
            Rule("Ле Лой", Daily, GetLeLoyArchiveName),
            Rule("AnimusVox", Weekly, ageLimitInDays: 14),
            Rule("Lazyhawk", Daily, SimpleArchiveName(2008)),
            Rule("Ping08", Weekly),
            Rule("The222anonim", Weekly, SimpleArchiveName(2014)),
            Rule("Meiræ", Weekly, d => "Архив"),
            Rule("Alex parker 1979", Weekly, SimpleArchiveName(2019)),
            Rule("Putnik", Weekly, ageLimitInDays:90),
            Rule("Birulik", TriMonthly, SimpleArchiveName(2017), ageLimitInDays: 90),
            Rule("Megitsune-chan", Daily),
        };

        public void Execute(IMediaWiki wiki, string[] commandLine)
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var rule in _rules)
            {
                if (!rule.ShouldRun(now.Date))
                    continue;

                var talkName = string.Format("Обсуждение участника:{0}", rule.UserName);

                var talks = new Talks(wiki.GetPage(talkName));
                var dayX = now.AddDays(-rule.AgeLimitInDays);
                var removed = new Talks();

                foreach (var talk in talks.ToArray())
                {
                    if (talk.LastActivity < dayX)
                    {
                        talks.Remove(talk);
                        removed.Add(talk);
                    }
                }

                if (!removed.Any())
                    continue;

                var archiveName = string.Format("{0}/{1}", talkName, rule.GetArchiveName(now.Date));
                wiki.Edit(archiveName, "\n\n" + removed.FullText, EditSummary, true);
                wiki.Edit(talkName, talks.FullText, $"[[{archiveName}|{EditSummary}]]");
            }
        }

        class ArchiveRule
        {
            public ArchiveRule(string userName, Predicate<DateTime> shouldRun, Func<DateTime, string> getArchiveName = null, int ageLimitInDays = 7)
            {
                UserName = userName;
                ShouldRun = shouldRun;
                GetArchiveName = getArchiveName ?? SimpleArchiveName();
                AgeLimitInDays = ageLimitInDays;
            }

            public string UserName { get; }
            public Predicate<DateTime> ShouldRun { get; }
            public int AgeLimitInDays { get; }
            public Func<DateTime, string> GetArchiveName { get; }
        }

        private static ArchiveRule Rule(string userName, Predicate<DateTime> shouldRun, Func<DateTime, string> getArchiveName = null, int ageLimitInDays = 7)
            => new ArchiveRule(userName, shouldRun, getArchiveName, ageLimitInDays);

        private static bool Daily(DateTime _) => true;
        private static bool Weekly(DateTime d) => d.DayOfWeek == DayOfWeek.Wednesday;
        private static bool TriMonthly(DateTime d) => d.Day == 1 && d.Month % 3 == 1;

        private static Func<DateTime, string> SimpleArchiveName(int archiveStartYear = 0)
        {
            return date => "Архив/" + (date.Year - archiveStartYear);
        }

        private static string GetLeLoyArchiveName(DateTime date)
        {
            Func<int> GetArchiveName = () =>
            {
                if (date.Year <= 2012)
                    throw new ArgumentOutOfRangeException();

                if (date.Year == 2013)
                    return 2;

                if (date.Year == 2014)
                    return date.Month <= 6 ? 3 : 4;

                return 5 + (date.Year - 2015) * 4 + (date.Month - 1) / 3;
            };

            return $"Архив{GetArchiveName()}";
        }
    }
}
