﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.TemplateTasks
{
    public partial class DewikifyModule : Modules.IModule
    {
        public const string CategoryName = "Википедия:К быстрому удалению:Девикифицировать";
        public const string TemplateName = "Девикифицировать вхождения";
        public const string Summary = "Автоматическая девикификация ссылок на удаленную страницу.";
        public const string SummaryWithTitle = "Автодевикификация [[{0}]].";
        private const string SeeAlsoSectionName = "См. также";
        private const string DisambigTemplateName = "неоднозначность";
        private const string NamesakeListTemplateName = "Список однофамильцев";

        public void Execute(IMediaWiki wiki, string[] commandLine)
        {
            var executor = new TemplateBasedTaskExecutor<SimpleTaskTemplate>(wiki, TemplateName, Summary, t => new SimpleTaskTemplate(t));
            foreach (var taskPage in wiki.GetPagesInCategory(CategoryName, MediaWiki.Namespace.Wikipedia))
            {
                executor.Run(taskPage, taskTemplate =>
                {
                    var title = taskTemplate.Title;

                    var allTitles = wiki.GetAllPageNames(title);
                    Dewikify(wiki, allTitles, title);

                    foreach (var t in allTitles.Skip(1))
                        wiki.Delete(t, string.Format("[[ВП:КБУ#П1]] [[{0}]]", title));
                });
            }
        }

        private void Dewikify(IMediaWiki wiki, string[] titles, string originalTitle)
        {
            Dewikify(wiki, titles, originalTitle, wiki.GetLinksTo(titles, MediaWiki.Namespace.Article), DewikifyLinkIn);
            Dewikify(wiki, titles, originalTitle, wiki.GetTransclusionsOf(titles, MediaWiki.Namespace.Article), RemoveTransclusionsIn);
        }

        private static void Dewikify(IMediaWiki wiki, string[] titles, string originalTitle, IDictionary<string, string[]> entries, Func<string, string, ParserUtils, string> dewikify)
        {
            var parser = new ParserUtils(wiki);
            var linkingPages = entries.Values.SelectMany(x => x).Distinct().ToArray();
            foreach (var page in wiki.GetPages(linkingPages).Values.Where(p => p != null))
            {
                var text = page.Text;
                foreach (var title in titles)
                    text = dewikify(text, title, parser);

                if (text != page.Text)
                    wiki.Edit(page.Title, text, string.Format(SummaryWithTitle, originalTitle));
            }
        }

        private string DewikifyLinkIn(string pageIn, string linkToDewikify, ParserUtils parser)
        {
            var links = ParserUtils.FindLinksTo(pageIn, linkToDewikify);
            var found = new List<WikiLink>();

            var isDisambig = parser.FindTemplates(pageIn, DisambigTemplateName).Any()
                || parser.FindTemplates(pageIn, NamesakeListTemplateName).Any();

            foreach (var link in links.ToArray())
            {
                if (isDisambig || ParserUtils.GetSectionName(links, link) == SeeAlsoSectionName)
                    found.Add(link); // whole line will be removed later (see below)
                else
                    links.Update(link, link.Text ?? link.Link);
            }
            var text = links.Text;

            // now removing whole lines
            return text.Remove(found.Select(x => ParserUtils.GetWholeLineAt(links, x)).Distinct());
        }

        private static string RemoveTransclusionsIn(string pageIn, string templateName, ParserUtils parser)
        {
            // remove templates
            var text = Remove(parser.FindTemplates(pageIn, templateName, false));

            // remove empty refs
            text = Remove(new PartiallyParsedWikiText<object>(text, EmptyRefRegex(), x => new()));

            return text;
        }

        private static string Remove<T>(PartiallyParsedWikiText<T> parsed)
            where T : class
        {
            foreach (var item in parsed.ToArray())
                parsed.Update(item, "");
            var text = parsed.Text;

            var emptyLines = parsed
                .Select(t => ParserUtils.GetWholeLineAt(parsed, t))
                .Where(r => r.Get(text).Trim() == "");

            return text.Remove(emptyLines);
        }

        [GeneratedRegex(@"<ref\s*>\s*</ref>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
        private static partial Regex EmptyRefRegex();
    }
}
