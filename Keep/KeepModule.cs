﻿using System.Linq;
using System.Text.RegularExpressions;
using ChieBot.Modules;

namespace ChieBot.Keep;
public partial class KeepModule : IModule
{
    public const string CategoryName = "Википедия:К удалению:Оставлено";
    public const string TemplateName = "Итог — оставлено";
    public const string Summary = "Автоматическое оставление статьи.";

    public const string ForDelTemplateName = "К удалению";
    public const string KeptTemplateName = "Оставлено";
    public const string RfdTitlePrefix = "Википедия:К удалению/";

    public void Execute(IMediaWiki wiki, string[] commandLine)
    {
        var parser = new ParserUtils(wiki);
        var executor = new TemplateBasedTaskExecutor(wiki, TemplateName, Summary);

        foreach (var taskPage in wiki.GetPagesInCategory(CategoryName, MediaWiki.Namespace.Wikipedia))
        {
            if (!taskPage.StartsWith(RfdTitlePrefix))
                continue;

            var rfdDate = taskPage[RfdTitlePrefix.Length..];
            if (rfdDate.Contains('/'))
                continue;

            executor.Run(taskPage, title =>
            {
                var page = wiki.GetLastRevision(title);
                if (page == null)
                    return;

                var talkPage = wiki.GetAssociatePageTitle(title)[title];
                if (!talkPage.Namespace.IsTalk())
                    return;

                // remove {{К удалению}}

                var parsedPage = parser.FindTemplates(page.Text, ForDelTemplateName);
                foreach (var template in parsedPage.ToArray())
                    parsedPage.Update(template, "");
                if (parsedPage.Any())
                    wiki.Edit(title, NoIncludeRegex().Replace(parsedPage.Text, ""), Summary, revId: page.Id);

                // add {{Оставлено}}

                var talk = wiki.GetLastRevision(talkPage.Title);
                var parsedTalk = parser.FindTemplates(talk.Text, KeptTemplateName);
                var keptTemplate = parsedTalk.FirstOrDefault();

                if (keptTemplate == null)
                {
                    keptTemplate = new() { Name = KeptTemplateName };
                    UpdateKeptTempate(keptTemplate, rfdDate, title);

                    wiki.Edit(talkPage.Title, keptTemplate.ToString() + "\n", Summary, false, revId: talk.Id);
                }
                else
                {
                    UpdateKeptTempate(keptTemplate, rfdDate, title);

                    parsedTalk.Update(keptTemplate, keptTemplate.ToString());
                    wiki.Edit(talkPage.Title, parsedTalk.Text, Summary, revId: talk.Id);
                }
            });
        }
    }

    [GeneratedRegex(@"<noinclude></noinclude>\n?")]
    private static partial Regex NoIncludeRegex();

    private static void UpdateKeptTempate(Template template, string date, string title)
    {
        var items = template.Args
            .Where(a => a.Name == null)
            .Select((a, i) => new { Date = a.Value, Title = template.Args.FirstOrDefault(x => x.Name == GetArgName(i))?.Value })
            .ToList();

        var newItem = new { Date = date, Title = title };

        var index = items.FindIndex(x => x.Date == date);
        if (index == -1)
        {
            items.Add(newItem);
        }
        else
        {
            var existing = items[index];
            if (existing.Title == title)
                return;

            items[index] = newItem;
        }

        template.Args.Clear();
        template.Args.AddRange(items.Select(x => new Template.Argument() { Value = x.Date }));
        template.Args.AddRange(items
            .Select((x, i) => new Template.Argument() { Name = GetArgName(i), Value = x.Title })
            .Where(x => x.Value != null));

        static string GetArgName(int index) => $"l{index + 1}";
    }
}
