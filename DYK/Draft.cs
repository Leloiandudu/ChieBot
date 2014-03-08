using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ChieBot.DYK
{
    class Draft : Section
    {
        private static readonly Regex LineStartedWithSmall = new Regex(@"^\s*<small\b.*$", RegexOptions.Compiled | RegexOptions.Multiline);

        public DateTime Date { get; set; }

        public string GetIssueText()
        {
            var text = Text;
            var small = LineStartedWithSmall.Match(text);
            if (small.Index == 0)
                text = text.Substring(small.Length).TrimStart();
            return text.Trim();
        }
    }

    class Drafts : SectionedArticle<Draft>
    {
        private static readonly Regex DraftHeader = new Regex(@"^==\s*Выпуск\s+(от\s+)?(?<date>\d+ \w+)", RegexOptions.Compiled);

        public Drafts(string fullText)
            : base(fullText)
        {
        }

        protected override bool InitSection(Draft draft)
        {
            var match = DraftHeader.Match(draft.Title);
            DateTime date;
            if (!match.Success || !Utils.TryParseIssueDate(match.Groups["date"].Value, out date))
            {
                Console.WriteLine("Не удалось распарсить дату выпуска `{0}`", draft.Title);
                return false;
            }
            draft.Date = date;
            return true;
        }

        public Draft this[DateTime date]
        {
            get { return _sections.SingleOrDefault(d => d.Date == date); }
        }
    }
}
