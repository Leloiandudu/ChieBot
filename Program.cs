using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ChieBot
{
    class Program
    {
        /// <summary>Moscow timezone.</summary>
        /// <remarks>Because of mono we have to search timezone by city name.</remarks>
        private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.GetSystemTimeZones().Single(tz => tz.DisplayName.Contains("Moscow"));

        /// <summary>Next issue must be within this time span, unless "-force" is specified.</summary>
        /// <remarks>Used to workaround cron inability to run every N days. Cron is instructed to run everyday instead.</remarks>
        private static readonly TimeSpan MaxLeftTime = TimeSpan.FromDays(0.5);

        /// <summary>Period between DYK (in days).</summary>
        const int DYKPeriod = 3;

        static void Main(string[] args)
        {
            var nextIssueDate = GetNearestIssueDate();
            var prevIssueDate = nextIssueDate.AddDays(-DYKPeriod);

            if (!args.Contains("-force") && (Now() - nextIssueDate).Duration() > MaxLeftTime)
                return;

            var ver = typeof(ChieBot.Program).Assembly.GetName().Version;
            var userAgent = string.Format("ChieBot/{0}.{1} (https://bitbucket.org/leloiandudu/chiebot; leloiandudu@gmail.com)", ver.Major, ver.Minor);
            var wiki = new MediaWiki(new Uri("http://ru.wikipedia.org/w/api.php"), userAgent);
            var dyk = new DYK.DidYouKnow(wiki);

            wiki.ReadOnly = !args.Contains("-live");

            var dir = new FileInfo(typeof(Program).Assembly.Location).DirectoryName;
            var creds = File.ReadAllLines(Path.Combine(dir, "credentials.txt"));
            wiki.Login(creds[0], creds[1]);

            var draft = dyk.PopDraft(nextIssueDate);
            dyk.ArchiveDraftTalk(nextIssueDate);
            dyk.ArchiveCurrent(prevIssueDate, nextIssueDate);
            dyk.SetCurrent(draft);
            dyk.RemoveMarkedFromNextIssue(nextIssueDate);
            dyk.RemoveFromPreparationTimetable(nextIssueDate);
        }

        private static DateTime GetNearestIssueDate()
        {
            // some date DYK took place
            var started = new DateTime(2013, 10, 19);

            // max time (in days) we could issue next DYK earlier
            const double threshold = 0.5;

            var elapsed = (Now() - started).TotalDays;
            var rounded = Math.Floor((elapsed + threshold) / DYKPeriod) * DYKPeriod;

            return started.AddDays(rounded);
        }

        private static DateTime Now()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);
        }
    }
}
