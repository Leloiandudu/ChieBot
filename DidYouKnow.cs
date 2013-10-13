using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ChieBot
{
    class DidYouKnow
    {
        private const string TemplateName = "Шаблон:Знаете ли вы";
        private const string ArchiveName = "Проект:Знаете ли вы/Архив рубрики/{0:yyyy-MM}";
        private const string DraftName = "Проект:Знаете ли вы/Черновик";
        private const string DraftTalkName = "Обсуждение проекта:Знаете ли вы/Черновик";
        private const string DraftTalkArchiveName = "Обсуждение проекта:Знаете ли вы/Черновик/{0}";

        private static readonly Regex Placeholder = new Regex(@"\s*<!-- BOT .*?-->\s*", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex LineStartedWithSmall = new Regex(@"^\s*<small\b.*$", RegexOptions.Compiled | RegexOptions.Multiline);

        private readonly MediaWiki _wiki;

        public DidYouKnow(MediaWiki wiki)
        {
            _wiki = wiki;
        }

        class Template
        {
            private readonly string _prefix;
            private string _text;
            private readonly string _postfix;

            public Template(string fullText)
            {
                var first = Placeholder.Match(fullText);
                if (!first.Success)
                    throw new DidYouKnowException("Открывающий placeholder не найден.");
                var index = first.Index + first.Length;

                var second = Placeholder.Match(fullText, index);
                if (!second.Success)
                    throw new DidYouKnowException("Закрывающий placeholder не найден.");

                _prefix = fullText.Substring(0, index);
                _text = fullText.Substring(index, second.Index - index);
                _postfix = fullText.Substring(second.Index);

                System.Diagnostics.Debug.Assert(fullText == FullText);
            }

            public string IssueText
            {
                get { return _text; }
                set { _text = value.Trim(); }
            }

            public string FullText
            {
                get { return _prefix + _text + _postfix; }
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

        class Draft : Section
        {
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

        public string GetCurrent()
        {
            return new Template(_wiki.GetPage(TemplateName)).IssueText;
        }

        public void SetCurrent(string text)
        {
            var template = new Template(_wiki.GetPage(TemplateName));
            template.IssueText = text;
            _wiki.Edit(TemplateName, template.FullText, "Автоматическая публикация выпуска.");
        }

        public void ArchiveCurrent(DateTime issueDate, DateTime archiveDate)
        {
            var titleFormat = (issueDate.Month != archiveDate.Month)
                ? "{0:d MMMM} — {1:d MMMM}"
                : "{0:%d} — {1:d MMMM}";

            _wiki.Edit(
                GetArchiveName(archiveDate),
                string.Format("== {0} ==\n\n{1}\n\n",
                    string.Format(titleFormat, issueDate, archiveDate),
                    GetCurrent()
                ),
                "Автоматическая архивация прошлого выпуска.",
                false
            );
        }

        public string PopDraft(DateTime date)
        {
            return PopDraft(date, DraftName, true).GetIssueText();
        }

        private string PopDraftTalk(DateTime date)
        {
            var draft = PopDraft(date, DraftTalkName, false);
            if (draft == null) return null;
            return draft.FullText;
        }

        private Draft PopDraft(DateTime date, string pageName, bool required)
        {
            var drafts = new Drafts(_wiki.GetPage(pageName));
            var draft = drafts[date];

            if (draft == null)
            {
                if (required)
                    throw new DidYouKnowException(string.Format("Черновик за {0} не найден.", date));
                else
                    return null;
            }

            drafts.Remove(draft);
            _wiki.Edit(pageName, drafts.FullText, "Автоматическая публикация выпуска.");

            return draft;
        }

        public bool ArchiveDraftTalk(DateTime date)
        {
            var draft = PopDraftTalk(date);
            if (draft == null) return false;

            _wiki.Edit(
                GetDraftTalkArchiveName(date),
                draft,
                "Автоматическая архивация обсуждения прошлого выпуска.",
                true
            );
            return true;
        }

        private string GetArchiveName(DateTime date)
        {
            return string.Format(ArchiveName, date);
        }

        private string GetDraftTalkArchiveName(DateTime date)
        {
            return string.Format(DraftTalkArchiveName, date.Year - 2011);
        }
    }

    [Serializable]
    public class DidYouKnowException : Exception
    {
        public DidYouKnowException() { }
        public DidYouKnowException(string message) : base(message) { }
        public DidYouKnowException(string message, Exception inner) : base(message, inner) { }
        protected DidYouKnowException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
