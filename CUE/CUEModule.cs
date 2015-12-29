using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChieBot.Modules;

namespace ChieBot.CUE
{
    /// <summary>
    /// Марафон КУЛ (Cleanup Editathon)
    /// </summary>
    class CUEModule : IModule
    {
        private const string PageName = "Википедия:КУЛ должен быть очищен/II/Статьи";
        private const string TalkPrefix = "Обсуждение:";
        private static readonly Regex TemplateRegex = new Regex(@"\{\{\s*Статья проекта:КУЛ\s*\|\s*участник\s*=\s*(?<User>.*?)\s*\}\}", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);

            var page = new SectionedArticle<ArticlesList>(wiki.GetPage(PageName));
            if (page.Count != 1)
                throw new Exception("More than one section.");
            var list = page[0];
            list.Parse();

            var articles = wiki.GetPages(wiki.GetPagesInCategory("Статьи проекта:КУЛ", 1), true);

            foreach (var article in articles)
            {
                if (!article.Key.StartsWith(TalkPrefix))
                    throw new Exception("Strange talk title: " + article.Key);

                var title = article.Key.Substring(TalkPrefix.Length);

                var row = list.Rows.FirstOrDefault(r => MediaWiki.TitlesEqual(r.Title, title));
                if (row == null)
                {
                    row = new ArticlesList.Row { Title = title };
                    list.Rows.InsertAfter(row, list.Rows.LastOrDefault());
                }

                var match = TemplateRegex.Match(article.Value.Text);
                if (!match.Success)
                    continue;
                row.User = match.Success ? match.Groups["User"].Value : "???";
            }

            list.Update();

            wiki.Edit(PageName, page.FullText, "Автоматическое обновление списка статей");
        }
    }

    class ArticlesList : Section
    {
        private static readonly Regex RowRegex = new Regex(@"\|-\s*\n\s*\|\s*\[\[\s*(?<Title>.+?)\s*\]\]\s*\|\|\s*\{\{u\|(?<User>.*?)\}\}\s*\|\|\s*(?<M1>\d*)\s*\|\|\s*(?<M2>\d*)\s*\|\|\s*(?<M3>\d*)\s*\|\|\s*(?<M4>\d*)\s*\|\|.+?\|\|(?<Data>.+?(?=\|-|\|\}))", RegexOptions.ExplicitCapture | RegexOptions.Singleline);
        private static readonly NumberFormatInfo NumberFormat = new NumberFormatInfo { NumberDecimalSeparator = "," };

        static ArticlesList()
        {
            NumberFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            NumberFormat.NumberDecimalSeparator = ",";
        }

        private PartiallyParsedWikiText<Row> _rows;

        public void Parse()
        {
            _rows = new PartiallyParsedWikiText<Row>(Text, RowRegex, m => new Row
            {
                Title = m.Groups["Title"].Value,
                User = m.Groups["User"].Value,
                Marks = Enumerable.Range(1, 4).Select(i => ParseMark(m.Groups["M" + i].Value)).ToArray(),                
                Data = m.Groups["Data"].Value,
            });
        }

        private int? ParseMark(string str)
        {
            if (str == "") return null;
            return int.Parse(str);
        }

        public void Update()
        {
            foreach (var row in Rows.ToArray())
            {
                _rows.Update(row, string.Format(NumberFormat, "|-\n| [[{0}]] || {{{{u|{1}}}}} || {2} || {3} || {4} || {5} || ''' {6:F2}''' ||{7}",
                    row.Title, row.User, row.Marks[0], row.Marks[1], row.Marks[2], row.Marks[3], row.Marks.Average(), row.Data));
            }
            Text = _rows.Text;
        }

        public PartiallyParsedWikiText<Row> Rows { get { return _rows; } }

        public class Row
        {
            public Row()
            {
                Marks = new int?[4];
                Data = "\n";
            }

            public string Title { get; set; }
            public string User { get; set; }
            public int?[] Marks { get; set; }
            public string Data { get; set; }
        }
    }
}
