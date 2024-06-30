using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.Archiving
{
    class Talk : Section
    {
        public DateTimeOffset? LastActivity { get; set; }
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
                .SelectMany(comment => DateRegex.Matches(comment))
                .Max(TryParseDate);
            return true;
        }

        private static readonly Regex DateRegex = new Regex(@"(\d{1,2}:\d{1,2}, \d+ \w+ \d+) \(UTC\)");

        private static DateTimeOffset? TryParseDate(Match match)
        {
            if (!match.Success)
                return null;
            return Utils.ParseDate(match.Groups[1].Value);
        }
    }
}
