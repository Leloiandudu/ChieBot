using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChieBot.Archiving
{
    class ArchiveModule : Modules.IModule
    {
        private const string EditSummary = "Автоматический аркайвинг";

        private readonly IDictionary<string, IArchiveRules> _rules = new IArchiveRules[]
        {
            new LeLoiArchiveRules(),
            new GeneralArchiveRules("AnimusVox"),
            new GeneralArchiveRules("Milez189") { AgeLimitInDays = 7 },
            new LazyhawkArchiveRules(),
            new GeneralArchiveRules("Ping08") { AgeLimitInDays = 7 },
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
            wiki.Edit(talkName, talks.FullText, EditSummary);
        }

        interface IArchiveRules
        {
            string UserName { get; }
            string GetArchiveName(DateTime date);
            int AgeLimitInDays { get; }
        }

        class LeLoiArchiveRules : IArchiveRules
        {
            private const string ArchiveName = "Архив{0}";

            public string UserName
            {
                get { return "Ле Лой"; }
            }

            public string GetArchiveName(DateTime date)
            {
                return string.Format(ArchiveName, GetArchiveNamePrefix(date));
            }

            private static int? GetArchiveNamePrefix(DateTime date)
            {
                if (date.Year <= 2012)
                    return null;

                if (date.Year == 2013)
                    return 2;

                if (date.Year == 2014)
                    return date.Month <= 6 ? 3 : 4;

                return 5 + (date.Year - 2015) * 4 + (date.Month - 1) / 3;
            }

            public int AgeLimitInDays
            {
                get { return 7; }
            }
        }

        class GeneralArchiveRules : IArchiveRules
        {
            private readonly string _userName;

            public GeneralArchiveRules(string userName)
            {
                _userName = userName;
                AgeLimitInDays = 14;
            }

            private const string ArchiveName = "Архив/{0}";

            public string UserName
            {
                get { return _userName; }
            }

            public string GetArchiveName(DateTime date)
            {
                return string.Format(ArchiveName, date.Year);
            }

            public int AgeLimitInDays { get; set; }
        }

        class LazyhawkArchiveRules : IArchiveRules
        {
            private const string ArchiveName = "Архив/{0}";

            public string UserName
            {
                get { return "Lazyhawk"; }
            }
            
            public string GetArchiveName(DateTime date)
            {
                return string.Format(ArchiveName, date.Year - 2008);
            }

            public int AgeLimitInDays
            {
                get { return 7; }
            }
        }
    }
}
