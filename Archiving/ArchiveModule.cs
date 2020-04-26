using System;
using System.Collections.Generic;
using System.Linq;

namespace ChieBot.Archiving
{
    class ArchiveModule : Modules.IModule
    {
        private const string EditSummary = "Автоматический аркайвинг";

        private readonly IDictionary<string, IArchiveRules> _rules = new IArchiveRules[]
        {
            new DelegateArchiveRules("Ле Лой", d => GetLeLoyArchiveName(d).ToString()),
            new GeneralArchiveRules("AnimusVox") { AgeLimitInDays = 14 },
            new GeneralArchiveRules("Milez189"),
            new GeneralArchiveRules("Lazyhawk") { ArchiveStartYear = 2008 },
            new GeneralArchiveRules("Ping08"),
            new GeneralArchiveRules("The222anonim") { ArchiveStartYear = 2014 },
            new GeneralArchiveRules("Пппзз") { ArchiveStartYear = 2016 },
            new GeneralArchiveRules("NoFrost"),
            new GeneralArchiveRules("Люба КБ"),
            new DelegateArchiveRules("Meiræ", d => ""),
            new GeneralArchiveRules("Stjn"),
            new GeneralArchiveRules("Red Blooded Man") { ArchiveStartYear = 2019 },
            new GeneralArchiveRules("Alex parker 1979") { ArchiveStartYear = 2019 },
        }.ToDictionary(x => x.UserName);

        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);

            if (commandLine.Length != 1)
                throw new Exception("specify user name as a single parameter");

            var rules = _rules[commandLine.Single()];
            var talkName = string.Format("Обсуждение участника:{0}", rules.UserName);

            var now = DateTimeOffset.UtcNow;
            var archiveName = string.Format("{0}/{1}", talkName, rules.GetArchiveName(now.Date));

            var talks = new Talks(wiki.GetPage(talkName));
            var dayX = now.AddDays(-rules.AgeLimitInDays);
            var removed = new Talks();

            foreach (var talk in talks.ToArray())
            {
                if (talk.LastActivity.HasValue && talk.LastActivity < dayX)
                {
                    talks.Remove(talk);
                    removed.Add(talk);
                }
            }

            if (!removed.Any())
                return;

            wiki.Edit(archiveName, "\n\n" + removed.FullText, EditSummary, true);
            wiki.Edit(talkName, talks.FullText, $"[[{archiveName}|{EditSummary}]]");
        }

        private static int GetLeLoyArchiveName(DateTime date)
        {
            if (date.Year <= 2012)
                throw new ArgumentOutOfRangeException();

            if (date.Year == 2013)
                return 2;

            if (date.Year == 2014)
                return date.Month <= 6 ? 3 : 4;

            return 5 + (date.Year - 2015) * 4 + (date.Month - 1) / 3;
        }

        interface IArchiveRules
        {
            string UserName { get; }
            string GetArchiveName(DateTime date);
            int AgeLimitInDays { get; }
        }

        abstract class ArchiveRulesBase : IArchiveRules
        {
            private readonly string _userName;

            public ArchiveRulesBase(string userName)
            {
                _userName = userName;
                AgeLimitInDays = 7;
            }

            public int AgeLimitInDays { get; set; }

            public string UserName { get { return _userName; } }

            protected abstract string GetArchiveName(DateTime date);

            string IArchiveRules.GetArchiveName(DateTime date)
            {
                return "Архив" + GetArchiveName(date);
            }
        }

        class DelegateArchiveRules : ArchiveRulesBase
        {
            private readonly Func<DateTime, string> _getArchiveName;

            public DelegateArchiveRules(string userName, Func<DateTime, string> getArchiveName)
                : base(userName)
            {
                _getArchiveName = getArchiveName;
            }

            protected override string GetArchiveName(DateTime date)
            {
                return _getArchiveName(date);
            }
        }

        class GeneralArchiveRules : ArchiveRulesBase
        {
            public GeneralArchiveRules(string userName)
                : base(userName)
            {
            }

            protected override string GetArchiveName(DateTime date)
            {
                return "/" + (date.Year - ArchiveStartYear);
            }

            public int ArchiveStartYear { get; set; }
        }
    }
}
