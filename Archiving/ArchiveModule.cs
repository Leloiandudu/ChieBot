using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChieBot.Archiving
{
    class ArchiveModule : Modules.IModule
    {
        private const string TalkName = "Обсуждение участника:Ле Лой";
        private const string ArchiveName = "Обсуждение участника:Ле Лой/Архив{0}";
        private const string EditSummary = "Автоматический аркайвинг";
        private const int AgeLimitInDays = 7;

        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);

            var now = DateTime.UtcNow;
            var archiveName = GetArchiveName(now);

            var talks = new Talks(wiki.GetPage(TalkName));
            var dayX = now.AddDays(-AgeLimitInDays);
            var removed = new Talks();

            var found = false;
            foreach (var talk in talks.ToArray())
            {
                if (talk.LastActivity.HasValue && talk.LastActivity < dayX)
                {
                    talks.Remove(talk);
                    removed.Add(talk);
                    found = true;
                }
            }

            if (!found)
                return;

            wiki.Edit(TalkName, talks.FullText, EditSummary);
            wiki.Edit(archiveName, "\n\n" + removed.FullText, EditSummary, true);
        }

        private static string GetArchiveName(DateTime date)
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
    }
}
