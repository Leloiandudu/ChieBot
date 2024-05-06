using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChieBot.DYK
{
    class DYKModule : Modules.IModule
    {
        /// <summary>Moscow timezone.</summary>
        /// <remarks>Because of mono we have to search timezone by city name.</remarks>
        private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.GetSystemTimeZones().Single(tz => tz.Id is "Russian Standard Time" or "Europe/Moscow");

        /// <summary>Next issue must be within this time span, unless "-force" is specified.</summary>
        /// <remarks>Used to workaround cron inability to run every N days. Cron is instructed to run everyday instead.</remarks>
        private static readonly TimeSpan MaxLeftTime = TimeSpan.FromDays(0.5);

        /// <summary>Period between DYK (in days).</summary>
        const int DYKPeriod = 3;

        public void Execute(IMediaWiki wiki, string[] args)
        {
            var nextIssueDate = GetNearestIssueDate();
            var prevIssueDate = nextIssueDate.AddDays(-DYKPeriod);

            if (!args.Contains("-force") && (Now() - nextIssueDate).Duration() > MaxLeftTime)
                return;

            var dyk = new DYK.DidYouKnow(wiki);
            var draft = dyk.PopDraft(nextIssueDate);
            dyk.ArchiveDraftTalk(prevIssueDate);
            dyk.ArchiveCurrent(prevIssueDate, nextIssueDate);
            dyk.SetCurrent(draft);
            dyk.RemoveMarkedFromNextIssue(nextIssueDate);
            dyk.RemoveFromPreparationTimetable(nextIssueDate);

            var issueValidUntil = nextIssueDate.AddDays(DYKPeriod);
            dyk.Stabilize(draft, new DateTimeOffset(issueValidUntil, TimeZone.GetUtcOffset(issueValidUntil)));
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
