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
        private const string RfdTitle = "Википедия:К удалению/{0:d MMMM yyyy}";
        private const string EditSummary = "Автоматическая простановка шаблона КУ.";
        private static readonly Regex NoIncludeRegex = new Regex(@"<(/)?noinclude(?:\s.*?)?>", RegexOptions.IgnoreCase);
        private static readonly Regex RfdTemplateRegex = new Regex(@"\{\{(К удалению|КУ)\|.*?\}\}", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        private static readonly string[] ResultTitles = { "Итог", "Автоитог" };

        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);

            var date = DateTime.UtcNow;
            var rfdPage = wiki.GetPage(string.Format(Utils.DateTimeFormat, RfdTitle, date));
            var links = GetArticles(rfdPage).Distinct().ToArray();

            foreach (var page in wiki.GetPages(links, true).Values.Where(page => page != null))
            {
                var match = RfdTemplateRegex.Match(page.Text);
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

        private static IEnumerable<string> GetArticles(string text)
        {
            return GetArticles(new SectionedArticle<Section>(text));
        }

        private static IEnumerable<string> GetArticles(SectionedArticle<Section> sections)
        {
            foreach (var section in sections)
            {
                var subSections = new SectionedArticle<Section>(section.Text, sections.Level + 1);
                if (subSections.Select(s => s.Title.TrimEnd().Trim('=').Trim()).Any(title => ResultTitles.Contains(title, StringComparer.InvariantCultureIgnoreCase)))
                    continue;

                foreach(var link in ParserUtils.FindAnyLinks(section.Title))
                    yield return link;

                if (sections.Level < 3)
                {
                    foreach (var article in GetArticles(subSections))
                        yield return article;
                }
            }

        }
    }
}
