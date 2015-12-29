using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChieBot.Modules;

namespace ChieBot.RFD
{
    /// <summary>
    /// К удалению (Requests for Deletion)
    /// </summary>
    class RFDModule : IModule
    {
        private const string CUTitle = "Википедия:К удалению/{0:d MMMM yyyy}";
        private const string EditSummary = "Автоматическая простановка шаблона КУ.";
        private static Regex NoIncludeRegex = new Regex(@"<(/)?noinclude(?:\s.*?)?>", RegexOptions.IgnoreCase);
        private static Regex CUTemplateRegex = new Regex(@"\{\{К удалению\|.*?\}\}", RegexOptions.IgnoreCase);
        private static Regex HeadingRegex = new Regex(@"^(={2,})(?<text>.*)\1", RegexOptions.Multiline);

        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);

            var date = DateTime.UtcNow;
            var cuPage = wiki.GetPage(string.Format(Utils.DateTimeFormat, CUTitle, date));

            var links = (
                from match in HeadingRegex.Matches(cuPage).Cast<Match>()
                select match.Groups["text"].Value into text
                from link in ParserUtils.FindAnyLinks(text)
                select link
            ).Distinct().ToArray();

            foreach (var page in wiki.GetPages(links, true).Values.Where(page => page != null))
            {
                var match = CUTemplateRegex.Match(page.Text);
                if (!match.Success)
                {
                    wiki.Edit(page.Title, string.Format("<noinclude>{{{{К удалению|{0:yyyy-MM-dd}}}}}</noinclude>\n", date), EditSummary, false);
                    continue;
                }

                var noInclCount = NoIncludeRegex.Matches(page.Text).Cast<Match>()
                    .TakeWhile(m => m.Index < match.Index)
                    .Select(m => m.Groups[1].Success)
                    .Aggregate(0, (count, closing) => count + (closing ? -1 : 1));

                if (noInclCount > 0)
                    continue;

                var text = page.Text.Substring(0, match.Index) + "<noinclude>" + match.Value + "</noinclude>" + page.Text.Substring(match.Index + match.Length);
                wiki.Edit(page.Title, text, EditSummary, null);
            }
        }
    }
}
