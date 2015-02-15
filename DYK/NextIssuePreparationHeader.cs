using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChieBot.DYK
{
    class NextIssuePreparationHeader : PartiallyParsedWikiText<NextIssuePreparationHeader.Item>
    {
        private static readonly Regex TimetableItem = new Regex(@"^\s*\|\s*\{\{злвч\|.*?\|(?<date>\d+ \w+)\|\+\}\}.*?\n\s*\|-\s*\n", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

        public NextIssuePreparationHeader(string text)
            : base(text, TimetableItem, x => new Item(x))
        {
        }

        public class Item
        {
            public Item(Match match)
            {
                DateTime date;
                if (!DYKUtils.TryParseIssueDate(match.Groups["date"].Value, out date))
                    throw new DidYouKnowException(string.Format("Не удалось распарсить дату выпуска `{0}`", match.Groups["date"].Value));
                Date = date;
            }

            public DateTime Date { get; private set; }
        }
    }
}
