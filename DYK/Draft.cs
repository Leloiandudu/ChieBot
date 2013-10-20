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
        private static readonly Regex DraftHeader = new Regex(@"^==\s*Выпуск\s+(?<date>\d+ \w+)", RegexOptions.Compiled);

        public Drafts(string fullText)
            : base(fullText)
        {
        }

        protected override void InitSection(Draft draft)
        {
            var match = DraftHeader.Match(draft.Title);
            DateTime date;
            if (!match.Success || !DateTime.TryParseExact(match.Groups["date"].Value, "d MMMM", CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out date))
                throw new DidYouKnowException(string.Format("Не удалось распарсить дату выпуска `{0}`", draft.Title));
            if ((DateTime.Now - date).TotalDays > 30) // на случай анонсов для следующего года
                date = date.AddYears(1);
            draft.Date = date;
        }

        public Draft this[DateTime date]
        {
            get { return _sections.SingleOrDefault(d => d.Date == date); }
        }
    }
}
