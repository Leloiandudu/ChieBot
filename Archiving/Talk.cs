using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChieBot.Archiving
{
    class Talk : Section
    {
        public DateTime? LastActivity { get; set; }
    }

    class Talks : SectionedArticle<Talk>
    {
        public Talks(string fullText = "")
            : base(fullText)
        {
        }

        protected override bool InitSection(Talk section)
        {
            section.LastActivity = section.FullText
                .Split('\n')
                .SelectMany(comment => DateRegex.Matches(comment).OfType<Match>())
                .Max(comment => TryParseDate(comment));
            return true;
        }

        private static readonly Regex DateRegex = new Regex(@"(\d{1,2}:\d{1,2}, \d+ \w+ \d+) \(UTC\)");

        private static DateTime? TryParseDate(Match match)
        {
            if (!match.Success)
                return null;
            return DateTime.Parse(match.Groups[1].Value, Utils.DateTimeFormat, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
    }
}
