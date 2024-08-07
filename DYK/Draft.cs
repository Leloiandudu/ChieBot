﻿using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.DYK
{
    class Draft : Section
    {
        private static readonly Regex LineStartedWithSmall = new Regex(@"^\s*<small\b.*$", RegexOptions.Compiled | RegexOptions.Multiline);

        public DateOnly Date { get; set; }

        public string GetIssueText()
        {
            var text = Text;
            var small = LineStartedWithSmall.Match(text);
            if (small.Index == 0)
                text = text[small.Length..].TrimStart();
            return text.Trim();
        }
    }

    class Drafts : SectionedArticle<Draft>
    {
        private static readonly Regex DraftHeader = new Regex(@"^==\s*(Выпуск\s+(от\s+)?)?(?<date>\d+ \w+)", RegexOptions.Compiled);

        public Drafts(string fullText)
            : base(fullText)
        {
        }

        protected override bool InitSection(Draft draft)
        {
            // don't parse remarks section
            if (draft.Title.Trim() == "== Примечания ==")
                return true;

            var match = DraftHeader.Match(draft.Title);
            if (!match.Success || !DYKUtils.TryParseIssueDate(match.Groups["date"].Value, out var date))
            {
                Console.Error.WriteLine("Не удалось распарсить дату выпуска `{0}`", draft.Title);
            }
            else
            {
                draft.Date = date;
            }
            return true;
        }

        public Draft this[DateOnly date]
        {
            get { return this.SingleOrDefault(d => d.Date == date); }
        }
    }
}
