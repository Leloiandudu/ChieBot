using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ChieBot.DYK
{
    class NextIssuePreparation : SectionedArticle<NextIssuePreparation.Item>
    {
        private static readonly Regex ArticleRegex = new Regex(@"(\{\{(?<status>" + Regex.Escape(DYKStatusTemplate.TemplateName) + @"\|[^}]+)\}\})?\s*\[\[(?<title>[^\]]+)\]\](,\s*)?", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        public NextIssuePreparation(string text)
            : base(text, 3)
        {
        }

        protected override bool InitSection(Item section)
        {
            section.Articles = new PartiallyParsedWikiText<Article>(section.Title, ArticleRegex, m => new Article(m));
            return true;
        }

        public void Update()
        {
            foreach (var item in this)
                item.Update();
        }

        public class Item : Section
        {
            private static readonly Regex CheckMark = new Regex(@"\{\{злвч\|.*?\|(?<date>\d+ \w+)\|*\}\}", RegexOptions.IgnoreCase);

            public PartiallyParsedWikiText<Article> Articles { get; set; }

            public DateTime? GetIssueDate()
            {
                var match = CheckMark.Match(Text);
                DateTime date;
                if (match.Success && DYKUtils.TryParseIssueDate(match.Groups["date"].Value, out date))
                    return date;
                return null;
            }

            public void Update()
            {
                Title = string.Format("=== {0} ===\r\n", string.Join(", ", Articles));
            }
        }

        public class Article
        {
            public Article(Match match)
            {
                var status = match.Groups["status"];
                Title = match.Groups["title"].Value;
                Status = status.Success ? new DYKStatusTemplate(status.Value) : null;
            }

            public string Title { get; private set; }

            public DYKStatusTemplate Status { get; set; }

            public override string ToString()
            {
                var result = string.Format("[[{0}]]", Title);
                if (Status != null)
                    result = Status.ToString() + " " + result;
                return result;
            }
        }
    }
}
