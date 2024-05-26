using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.DYK
{
    class NextIssuePreparation
    {
        private static readonly Regex ArticleRegex = new Regex(@"(\{\{(?<status>" + Regex.Escape(DYKStatusTemplate.TemplateName) + @"\|[^}]+)\}\})?\s*\[\[:?(?<title>[^|\]]+)(\|[^\]]+)?\]\](,\s*)?", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        private readonly SectionedArticle<Section> _page;
        public SectionedArticle<Item> Sections { get; private set; }
        public SectionedArticle<Item> NewSections { get; private set; }

        public NextIssuePreparation(string text)
        {
            _page = new SectionedArticle<Section>(text, 2);
            Sections = new Preparation(_page.Prefix);
            if (_page.Count > 1)
                NewSections = new Preparation(_page.Last().Text);
        }

        public void Update()
        {
            foreach (var item in Sections)
                item.Update();
            _page.Prefix = Sections.FullText;
            if (NewSections != null)
                _page.Last().Text = NewSections.FullText;
        }

        public string FullText
        {
            get { return _page.FullText; }
        }

        class Preparation : SectionedArticle<Item>
        {
            public Preparation(string text)
                : base(text, 3)
            {
            }

            protected override bool InitSection(Item section)
            {
                section.Articles = new PartiallyParsedWikiText<Article>(section.Title, ArticleRegex, m => new Article(m));
                return true;
            }
        }

        public class Item : Section
        {
            private static readonly Regex CheckMark = new Regex(@"\{\{злвч\|.*?\|\s*(?<date>\d+ \w+)\s*\|*\}\}", RegexOptions.IgnoreCase);

            public PartiallyParsedWikiText<Article> Articles { get; set; }

            public DateOnly? GetIssueDate()
            {
                var match = CheckMark.Match(Text);
                DateOnly date;
                if (match.Success && DYKUtils.TryParseIssueDate(match.Groups["date"].Value, out date))
                    return date;
                return null;
            }

            public void Update()
            {
                Title = string.Format("=== {0} ===\n", string.Join(", ", Articles));
            }
        }

        public class Article
        {
            public Article(Match match)
            {
                var status = match.Groups["status"];
                Title = match.Groups["title"].Value;

                try
                {
                    if (status.Success)
                        Status = new DYKStatusTemplate(status.Value);
                }
                catch
                {
                    Status = null;
                }
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
