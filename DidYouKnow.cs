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

        private static readonly Regex Heading2 = new Regex(@"^==.*==\s*$\n?", RegexOptions.Compiled | RegexOptions.Multiline);
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

        public void Archive(DateTime issueDate, DateTime archiveDate)
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

        public string GetDraft()
        {
            var text = _wiki.GetPage(DraftName);

            var first = Heading2.Match(text);
            if (!first.Success)
                throw new DidYouKnowException("Черновик не найден.");
            var index = first.Index + first.Length;

            var second = Heading2.Match(text, index);

            if (second.Success)
                text = text.Substring(index, second.Index - index);
            else
                text = text.Substring(index);
            text = text.Trim();

            var small = LineStartedWithSmall.Match(text);
            if (small.Index == 0)
                text = text.Substring(small.Length).TrimStart();

            return text;
        }

        private string GetArchiveName(DateTime date)
        {
            return string.Format(ArchiveName, date);
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
