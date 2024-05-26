using System;
using System.Text.RegularExpressions;

namespace ChieBot.DYK
{
    class NextIssuePreparationHeader : PartiallyParsedWikiText<NextIssuePreparationHeader.Item>
    {
        private static readonly Regex TimetableItem = new Regex(@"^\s*\|\s*\{\{злвч\|.*?\|\s*(?<date>\d+ \w+)\s*\|\+\}\}.*?\n\s*\|-\s*\n", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        public NextIssuePreparationHeader(string text)
            : base(text, TimetableItem, x => new Item(x))
        {
        }

        public class Item
        {
            public Item(Match match)
            {
                if (!DYKUtils.TryParseIssueDate(match.Groups["date"].Value, out var date))
                    throw new DidYouKnowException(string.Format("Не удалось распарсить дату выпуска `{0}`", match.Groups["date"].Value));
                Date = date;
            }

            public DateOnly Date { get; private set; }
        }
    }
}
