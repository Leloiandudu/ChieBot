using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ChieBot.ES
{
    /// <summary>
    /// Editathon Stats updater
    /// </summary>
    class ESModule : Modules.IModule
    {
        private const string PageNamePrefix = "Википедия:КУЛ должен быть очищен/III";
        private const string TemplateName = "Статья проекта:КУЛ";
        private const string ResultsTail = "\n[[Категория:Статьи проекта:КУЛ]]";

        private const string PageName = PageNamePrefix + "/Статьи";
        private const string StatsName = PageNamePrefix + "/marks.js";
        private const int TemplateInclusionNamespaceId = 1;
        private const string TemplateInclusionNamespaceName = "Обсуждение:";

        private static readonly Regex TemplateRegex = new Regex(@"{{\s*" + Regex.Escape(TemplateName) + @".*?}}\s*", RegexOptions.IgnoreCase);
        private static readonly NumberFormatInfo NumberFormat = new NumberFormatInfo { NumberDecimalSeparator = "," };

        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);

            var titles = wiki.GetPageTransclusions("Template:" + TemplateName, TemplateInclusionNamespaceId);
            UpdateResults(wiki, titles);
            //RemoveTemplate(wiki, titles);
        }

        private void RemoveTemplate(MediaWiki wiki, string[] titles)
        {
            var articles = wiki.GetPages(titles);
            foreach (var title in titles)
            {
                var text = articles[title].Text;
                var match = TemplateRegex.Match(text);
                text = match.Replace(text, "");
                wiki.Edit(title, text, "Автоматическое удаление шаблона марафона.");
            }
        }

        private void UpdateResults(MediaWiki wiki, string[] titles)
        {
            var marks = JObject.Parse(wiki.GetPage(StatsName));
            var articles = wiki.GetPages(titles);

            var table = new StringWriter(NumberFormat);
            table.WriteLine("{| class='wikitable sortable'");
            table.WriteLine("|-");
            table.WriteLine("! style='width: 25%' | Статья !! Участник !! {0} !! Итого !! class='unsortable' | Комментарии жюри",
                string.Join(" !! ", marks.Properties().Select(p => "{{nobr|{{u|" + p.Name + "}}}}")));
            foreach (var t in titles)
            {
                var title = t;
                if (title.StartsWith(TemplateInclusionNamespaceName))
                    title = title.Substring(TemplateInclusionNamespaceName.Length);

                table.WriteLine("|-");
                table.WriteLine("| [[{0}]] || {{{{u|{1}}}}} || {2} || ''' {3:F2} ''' || {4}",
                    title, GetSubmitter(articles[t].Text), FormatMarks(marks, title), GetMark(marks, title), GetComments(marks, title));
            }
            table.WriteLine("|}");

            var page = new SectionedArticle<Section>(wiki.GetPage(PageName));
            page[0].Text = table.ToString() + ResultsTail;
            wiki.Edit(PageName, page.FullText, "Автоматическое обновление страницы марафона.");
        }

        private string GetSubmitter(string text)
        {
            var match = TemplateRegex.Match(text);
            if (!match.Success)
                return "";

            var template = Template.Parse(match.Value.TrimEnd());
            return template.Args.Select(a => a.Value).FirstOrDefault() ?? "";
        }

        private string FormatMarks(JObject marks, string title)
        {
            return AggregateMarks(marks, title, (m, j) => FormatMark(m), m => string.Join(" || ", m));
        }

        private string FormatMark(JObject mark)
        {
            if (mark == null)
                return "";

            var marks = GetExistingMarks(mark).Select(m => m.Title);

            return string.Join("", marks);
        }

        private double? GetMark(JObject marks, string title)
        {
            return AggregateMarks(marks, title,
                (m, j) =>
                {
                    if (m == null)
                        return null;
                    return GetExistingMarks(m).Select(mi => (double?)mi.Value).Sum();
                },
                m => m.Average());
        }

        private string GetComments(JObject marks, string title)
        {
            return AggregateMarks(marks, title,
                (m, j) =>
                {
                    if (m == null)
                        return null;
                    var comment = m.Value<string>("comment");
                    if (string.IsNullOrEmpty(comment))
                        return null;
                    return string.Format("<small>'''{0}''': {1}</small>", j, comment);
                },
                m => string.Join("\n\n", m.Where(c => c != null)));
        }

        private static MarkInfo[] GetExistingMarks(JObject mark)
        {
            var value = mark.Value<int>("mark");
            return new[] { new MarkInfo(value, value.ToString()) };
        }

        private static T AggregateMarks<T>(JObject marks, string title, Func<JObject, string, T> get, Func<IEnumerable<T>, T> aggregate)
        {
            return aggregate(marks.Properties().Select(p => get(p.Value.Value<JObject>(title), p.Name)));
        }

        class MarkInfo
        {
            public MarkInfo(int value, string title)
            {
                Value = value;
                Title = title;
            }

            public int Value { get; private set; }
            public string Title { get; private set; }
        }
    }
}
