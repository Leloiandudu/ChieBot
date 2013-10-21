using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ChieBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var ver = typeof(ChieBot.Program).Assembly.GetName().Version;
            var userAgent = string.Format("ChieBot/{0}.{1} (https://bitbucket.org/leloiandudu/chiebot; leloiandudu@gmail.com)", ver.Major, ver.Minor);
            var wiki = new MediaWiki(new Uri("https://ru.wikipedia.org/w/api.php"), userAgent);
            var dyk = new DYK.DidYouKnow(wiki);

            var dir = new FileInfo(typeof(Program).Assembly.Location).DirectoryName;
            var creds = File.ReadAllLines(Path.Combine(dir, "credentials.txt"));
            wiki.Login(creds[0], creds[1]);

            var nextIssueDate = new DateTime(2013, 10, 22);
            var prevIssueDate = nextIssueDate.AddDays(-3);

            var draft = dyk.PopDraft(nextIssueDate);
            var draftTalk = dyk.ArchiveDraftTalk(prevIssueDate);
            dyk.ArchiveCurrent(prevIssueDate, nextIssueDate);
            dyk.ArchiveCurrentTalk(prevIssueDate);
            dyk.SetCurrent(draft);
            dyk.RemoveMarkedFromNextIssue(nextIssueDate);
            dyk.RemoveFromPreparationTimetable(nextIssueDate);
        }
    }
}
