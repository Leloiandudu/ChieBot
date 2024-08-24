using System.Text.RegularExpressions;

namespace ChieBot.DYK
{
    partial class DykTemplate
    {
        [GeneratedRegex(@"\s*<!--\s*BOT\s+.*?-->\s*", RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex Placeholder();

        private readonly string _prefix;
        private string _text;
        private readonly string _postfix;

        public DykTemplate(string fullText)
        {
            var first = Placeholder().Match(fullText);
            if (!first.Success)
                throw new DidYouKnowException("Открывающий placeholder не найден.");
            var index = first.Index + first.Length;

            var second = Placeholder().Match(fullText, index);
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
}
