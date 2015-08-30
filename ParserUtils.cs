using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot
{
    static class ParserUtils
    {
        private static readonly Regex BoldLinkRegex = new Regex(@"('''[^\[\]']+''')|('''.*?\[\[(?<link>[^|\]]+)(\|[^\]]+)?\]\].*?('''|$))|(\[\[(?<link>[^|\]]+)\|'''.*?'''\]\])", RegexOptions.ExplicitCapture);
        private static readonly Regex NonArticleLinksRegex = new Regex(@"^(User|У|Участник|User talk|ОУ|Обсуждение участника|ВП|Википедия)\:", RegexOptions.ExplicitCapture);
        private static readonly Regex LinkHashRegex = new Regex(@"^(?<link>[^#]+)(#.*)?$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Returns names of all articles from main namespace, linked in bold in the specified <paramref name="text" />.
        /// </summary>
        public static string[] FindBoldLinks(string text)
        {
            return BoldLinkRegex
                .Matches(text).OfType<Match>()
                .Select(m => m.Groups["link"])
                .Where(g => g.Success)
                .Select(g => g.Value)
                .Where(l => !NonArticleLinksRegex.IsMatch(l))
                .Select(l => LinkHashRegex.Match(l).Groups["link"].Value)
                .ToArray();
        }
    }
}
