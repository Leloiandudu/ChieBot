﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private static readonly Regex RfdTemplateRegex = new Regex(@"\{\{(К удалению|КУ)\|?(?<date>.*?)\}\}", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        private static readonly Regex RedirectRegex = new Regex(@"^\s*#(redirect|перенаправление)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly string[] ResultTitles = { "Итог", "Автоитог" };
        private const string CategoryName = "Категория:Википедия:Кандидаты на удаление";

        public void Execute(IMediaWiki wiki, string[] commandLine)
        {
            var date = DateTime.UtcNow;
            if (commandLine.Length == 1)
                date = DateTime.Parse(commandLine[0], Utils.DateTimeFormat, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

            var rfdTitle = string.Format(Utils.DateTimeFormat, RfdTitle, date);
            var history = Revision.FromHistory(wiki.GetHistory(rfdTitle, DateTimeOffset.MinValue) ?? new MediaWiki.RevisionInfo[0]);

            var rfdPage = history.FirstOrDefault()?.GetText(wiki);
            if (rfdPage == null)
            {
                wiki.Edit(rfdTitle, "{{КУ-Навигация}}", "Создание и оформление страницы нового дня");
                return;
            }

            var links = GetArticles(rfdPage).Distinct().ToArray();
            var categories = wiki.GetPagesCategories(links, false);
            links = links.Where(title => !categories[title].Contains(CategoryName)).ToArray();

            var ts = DateTime.UtcNow;
            var pages = wiki.GetPages(links, false);
            wiki.BotFlag = false;

            foreach (var link in pages)
            {
                var page = link.Value;
                if (page == null)
                    continue;

                // skip modules & css
                if (page.Title.StartsWith("Модуль:") || page.Title.EndsWith(".css"))
                    continue;

                var createdBy = history.FindEarliest(wiki, text => GetArticles(text).Contains(link.Key)).Info;
                if (createdBy.Anonymous) continue;

                var newText = string.Format("<noinclude>{{{{К удалению|{0:yyyy-MM-dd}}}}}</noinclude>", date);
                var match = RfdTemplateRegex.Match(page.Text);

                if (!match.Success)
                {
                    // if a page starts with a table - add newline after the template or the table will break
                    if (page.Text.StartsWith("{|"))
                        newText += "\n";

                    var isRedirect = RedirectRegex.IsMatch(page.Text);
                    if (isRedirect)
                        newText = "\n" + newText;

                    wiki.Edit(page.Title, newText, EditSummary, isRedirect);
                    continue;
                }

                if (match.Groups["date"].Length == 0)
                {
                    wiki.Edit(page.Title, match.Replace(page.Text, newText), EditSummary);
                    continue;
                }

                if (!RequiresNoInclude(page.Title, categories[link.Key]))
                    continue;

                if (IsNoIncludeOpen(page.Text, match.Index))
                    continue;

                wiki.Edit(page.Title, match.Replace(page.Text, "<noinclude>" + match.Value + "</noinclude>"), EditSummary, null, ts);
            }
        }

        private static bool IsNoIncludeOpen(string text, int atIndex)
        {
            return NoIncludeRegex.Matches(text)
                .TakeWhile(m => m.Index < atIndex)
                .Select(m => m.Groups[1].Success)
                .Sum(closing => closing ? -1 : 1) > 0;
        }

        private bool RequiresNoInclude(string title, string[] categories)
        {
            return title.StartsWith("Шаблон:")
                || categories.Contains("Категория:Страницы разрешения неоднозначностей по алфавиту");
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
