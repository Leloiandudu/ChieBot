using System;
using System.Linq;

namespace ChieBot.DYK
{
    public class DYKModule : Modules.IModule
    {
        /// <summary>Moscow timezone.</summary>
        public static TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

        /// <summary>Next issue must be within this time span, unless "-force" is specified.</summary>
        /// <remarks>Used to workaround cron inability to run every N days. Cron is instructed to run everyday instead.</remarks>
        private static readonly TimeSpan MaxLeftTime = TimeSpan.FromDays(0.5);

        public void Execute(IMediaWiki wiki, string[] args)
        {
            var nextIssueDate = GetNearestIssueDate();

            if (!args.Contains("-force") && (ExecutionTime - nextIssueDate).Duration() > MaxLeftTime)
                return;

            var dyk = new DYK.DidYouKnow(wiki, nextIssueDate);
            var draft = dyk.PopDraft();
            dyk.ArchiveDraftTalk();
            dyk.ArchiveCurrent();
            dyk.SetCurrent(draft);
            dyk.RemoveMarkedFromNextIssue();
            dyk.RemoveFromPreparationTimetable();
            dyk.Stabilize(draft);
        }

        private DateTimeOffset GetNearestIssueDate()
        {
            // some date DYK took place
            var started = new DateTime(2013, 10, 19).WithTimeZone(TimeZone);

            // max time (in days) we could issue next DYK earlier by
            const double threshold = 0.5;

            var elapsed = (ExecutionTime - started).TotalDays;
            var rounded = Math.Floor((elapsed + threshold) / DidYouKnow.PeriodInDays) * DidYouKnow.PeriodInDays;

            return started.AddDays(rounded).Date.WithTimeZone(TimeZone);
        }

        public DateTimeOffset ExecutionTime { get; set; } = DateTimeOffset.UtcNow;
    }
}
