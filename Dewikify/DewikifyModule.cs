﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChieBot.Dewikify
{
    class DewikifyModule : Modules.IModule
    {
        private const int TemplateNamespaceId = 4; // Википедия
        private const int DewikifyNamespaceId = 0; // main ns
        private const string CategoryName = "Википедия:К быстрому удалению:Девикифицировать";
        private const string TemplateName = "Девикифицировать вхождения";
        private const string Summary = "Автоматическая девикификация ссылок на удаленную статью.";
        private const string RemovalSummary = "[[ВП:КБУ#П1]]";
        private static readonly string[] IncludeGroups = { "sysop", "closer" };
        private static readonly string[] ExcludeGroups = { "bot" };
        private const string ClosingSectionName = "Итог";
        private const string SeeAlsoSectionName = "См. также";
        private const string DisambigTemplateName = "неоднозначность";
        private const string NamesakeListTemplateName = "Список однофамильцев";

        private static readonly Regex HeaderRegex = new Regex(@"^=+\s*([^=].*?)\s*=+", RegexOptions.Multiline);

        private readonly Dictionary<string, bool> _powerUsers = new Dictionary<string, bool>();

        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);

            var titles = wiki.GetPagesInCategory(CategoryName, TemplateNamespaceId);
            foreach (var title in titles)
            {
                var history = wiki.GetHistory(title, DateTimeOffset.MinValue).Reverse().Select(r => new Revision(r)).ToArray();

                LoadUsers(wiki, history);

                var page = ParserUtils.FindTemplates(history.First().GetText(wiki), TemplateName);
                foreach (var dt in page.Select(t => new DewikifyTemplate(t)).ToArray())
                {
                    if (dt.Error != null)
                    {
                        page.Update(dt.Template, string.Format("<span style='color: red'>Ошибка в шаблоне <nowiki>{0}</nowiki>: '''{1}'''</span>", dt.Template.ToString(), dt.Error));
                        continue;
                    }

                    if (dt.IsDone)
                    {
                        continue;
                    }

                    var section = GetSectionName(page, dt.Template);
                    if (section != ClosingSectionName)
                    {
                        page.Update(dt.Template, string.Format("<span style='color: red'>Шаблон <nowiki>{0}</nowiki> должен находиться в секции '''Итоги'''.</span>", dt.Template.ToString()));
                        continue;
                    }

                    var user = GetUser(wiki, dt, history);
                    if (!_powerUsers[user])
                    {
                        page.Update(dt.Template, string.Format("<span style='color: red'>Шаблон <nowiki>{0}</nowiki> установлен пользователем {{{{u|{1}}}}}, не имеющим флага ПИ/А.</span>", dt.Template.ToString(), user));
                        continue;
                    }

                    var allTitles = wiki.GetAllPageNames(dt.Title);
                    DewikifyLinksTo(wiki, allTitles);
                    RemoveTransclusionsOf(wiki, allTitles);

                    foreach (var t in allTitles.Skip(1))
                        wiki.Delete(t, RemovalSummary);

                    dt.IsDone = true;
                    page.Update(dt.Template, dt.Template.ToString());
                }

                if (history.First().GetText(wiki) != page.Text)
                    wiki.Edit(title, page.Text, Summary);
            }
        }

        private static string GetSectionName<T>(PartiallyParsedWikiText<T> page, T item)
            where T : class
        {
            var offset = page.GetOffset(item);
            return HeaderRegex.Matches(page.Text)
                .Cast<Match>()
                .TakeWhile(m => m.Index < offset)
                .Select(m => m.Groups[1].Value.Trim())
                .LastOrDefault();
        }

        private void LoadUsers(MediaWiki wiki, Revision[] history)
        {
            var users = wiki.GetUserGroups(history.Select(h => h.User).Distinct().Except(_powerUsers.Keys).ToArray());
            foreach (var user in users)
            {
                var groups = user.Value;
                _powerUsers.Add(user.Key, groups.Any(g => IncludeGroups.Contains(g)) && groups.All(g => !ExcludeGroups.Contains(g)));
            }
        }

        private string GetUser(MediaWiki wiki, DewikifyTemplate template, Revision[] history)
        {
            // looking for the first edit where the template did not exist
            var user = history.First().User;
            foreach (var revBatch in history.Skip(1).Partition(10))
            {
                Revision.LoadText(wiki, revBatch);
                foreach (var rev in revBatch)
                {
                    var exists = ParserUtils.FindTemplates(rev.GetText(wiki), TemplateName)
                        .Select(t => new DewikifyTemplate(t))
                        .Where(t => t.Error == null)
                        .Any(t => t.Title == template.Title);

                    if (!exists)
                        return user;

                    user = rev.User;
                }
            }
            return user;
        }

        private static void DewikifyLinksTo(MediaWiki wiki, string[] titles)
        {
            foreach (var linkingPages in wiki.GetLinksTo(titles, DewikifyNamespaceId))
            {
                foreach (var page in wiki.GetPages(linkingPages.Value).Values)
                {
                    var text = DewikifyLinkIn(page.Text, linkingPages.Key);
                    if (text != page.Text)
                        wiki.Edit(page.Title, text, Summary);
                }
            }
        }

        private static string DewikifyLinkIn(string pageIn, string linkToDewikify)
        {
            var links = ParserUtils.FindLinksTo(pageIn, linkToDewikify);
            var found = new List<WikiLink>();

            var isDisambig = ParserUtils.FindTemplates(pageIn, DisambigTemplateName).Any()
                || ParserUtils.FindTemplates(pageIn, NamesakeListTemplateName).Any();

            foreach (var link in links.ToArray())
            {
                if (isDisambig || GetSectionName(links, link) == SeeAlsoSectionName)
                    found.Add(link); // whole line will be removed later (see below)
                else
                    links.Update(link, link.Text ?? link.Link);
            }
            var text = links.Text;

            // now removing whole lines
            return text.Remove(found.Select(x => ParserUtils.GetWholeLineAt(links, x)).Distinct());
        }

        private static void RemoveTransclusionsOf(MediaWiki wiki, string[] titles)
        {
            foreach (var transcludingPages in wiki.GetPageTransclusions(titles, DewikifyNamespaceId))
            {
                foreach (var page in wiki.GetPages(transcludingPages.Value).Values)
                {
                    var text = RemoveTransclusionsIn(page.Text, transcludingPages.Key);
                    if (text != page.Text)
                        wiki.Edit(page.Title, text, Summary);
                }
            }
        }

        private static string RemoveTransclusionsIn(string pageIn, string templateName)
        {
            var templates = ParserUtils.FindTemplates(pageIn, templateName, false);
            foreach (var template in templates.ToArray())
                templates.Update(template, "");
            var text = templates.Text;

            var emptyLines = templates
                .Select(t => ParserUtils.GetWholeLineAt(templates, t))
                .Where(r => r.Get(text).Trim() == "");

            return text.Remove(emptyLines);
        }

        class DewikifyTemplate
        {
            private const string DoneArg = "сделано";

            private readonly Template _template;
            private string _error;

            public DewikifyTemplate(Template template)
            {
                _template = template;
                Verify();
            }

            private void Verify()
            {
                if (_template.Args.Count == 2 && _template.Args[1].Name == null && _template.Args[1].Value == DoneArg)
                    return;

                if (_template.Args.Count != 1 || _template.Args[0].Name != null)
                    _error = "неверый формат аргументов";
            }

            public Template Template
            {
                get { return _template; }
            }

            public string Error
            {
                get { return _error; }
            }

            public string Title
            {
                get
                {
                    Check();
                    return _template.Args[0].Value;
                }
            }

            public bool IsDone
            {
                get
                {
                    Check();
                    return _template.Args.Count == 2;
                }
                set
                {
                    if (IsDone == value) return;
                    if (value)
                        _template.Args.Add(new Template.Argument { Value = DoneArg });
                    else
                        _template.Args.RemoveAt(1);
                }
            }

            private void Check()
            {
                if (_error != null)
                    throw new InvalidOperationException("Template is not valid: " + _error);
            }
        }

        [DebuggerDisplay("#{Id} {User,nq}")]
        class Revision
        {
            private readonly MediaWiki.RevisionInfo _rev;
            private string _text;

            public Revision(MediaWiki.RevisionInfo rev)
            {
                _rev = rev;
            }

            public int Id { get { return _rev.Id; } }

            public string User
            {
                get { return _rev.User; }
            }

            public string GetText(MediaWiki wiki)
            {
                if (_text == null)
                    _text = wiki.GetPage(_rev.Id);
                return _text;
            }

            public static void LoadText(MediaWiki wiki, IEnumerable<Revision> revisions)
            {
                revisions = revisions.Where(r => r._text == null).ToArray();
                var texts = wiki.GetPages(revisions.Select(r => r.Id).ToArray());
                foreach (var rev in revisions)
                    rev._text = texts[rev.Id];
            }
        }
    }
}
