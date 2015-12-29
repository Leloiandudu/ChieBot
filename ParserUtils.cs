using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot
{
    static class ParserUtils
    {
        private static readonly Regex LinkRegex = new Regex(@"\[\[:?(?<link>[^|\]]+)(\|[^\]]+)?\]\]", RegexOptions.ExplicitCapture);
        private static readonly Regex BoldLinkRegex = new Regex(@"('''[^\[\]']+''')|('''.*?\[\[:?(?<link>[^|\]]+)(\|[^\]]+)?\]\].*?('''|$))|(\[\[:?(?<link>[^|\]]+)\|'''.*?'''\]\])", RegexOptions.ExplicitCapture);
        private static readonly Regex NonArticleLinksRegex = new Regex(@"^(User|У|Участник|User talk|ОУ|Обсуждение участника|ВП|Википедия|File|Image|Файл|Category|Категория|К|Template|Шаблон|Ш)\:", RegexOptions.ExplicitCapture);
        private static readonly Regex LinkHashRegex = new Regex(@"^(?<link>[^#]+)(#.*)?$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Returns names of all articles from main namespace, linked in bold in the specified <paramref name="text" />.
        /// </summary>
        public static string[] FindBoldLinks(string text)
        {
            return GetLinks(BoldLinkRegex.Matches(text));
        }

        /// <summary>
        /// Returns names of all articles from main namespace, linked in the specified <paramref name="text" />.
        /// </summary>
        public static string[] FindLinks(string text)
        {
            return GetLinks(LinkRegex.Matches(text));
        }

        public static string[] FindAnyLinks(string text)
        {
            return LinkRegex.Matches(text).Cast<Match>()
                .Select(m => m.Groups["link"].Value)
                .ToArray();
        }

        private static string[] GetLinks(MatchCollection matches)
        {
            return matches.Cast<Match>()
                .Select(m => m.Groups["link"])
                .Where(g => g.Success)
                .Select(g => g.Value)
                .Where(l => !NonArticleLinksRegex.IsMatch(l))
                .Select(l => LinkHashRegex.Match(l).Groups["link"].Value)
                .ToArray();
        }
    }
}
