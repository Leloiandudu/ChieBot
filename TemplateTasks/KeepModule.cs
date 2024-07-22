using System.Linq;
using System.Text.RegularExpressions;
using ChieBot.Modules;

#nullable enable

namespace ChieBot.TemplateTasks;
public class KeepModule : IModule
{
    public const string CategoryName = "Википедия:К удалению:Оставлено";
    public const string TemplateName = "Итог — оставлено";
    public const string Summary = "Автоматическое оставление статьи.";

    public const string KeptTemplateName = "Оставлено";
    public const string RfdTitlePrefix = "Википедия:К удалению/";

    public void Execute(IMediaWiki wiki, string[] commandLine)
    {
        var parser = new ParserUtils(wiki);
        var executor = new TemplateBasedTaskExecutor<SimpleTaskTemplate>(wiki, TemplateName, Summary, t => new SimpleTaskTemplate(t));

        foreach (var taskPage in wiki.GetPagesInCategory(CategoryName, MediaWiki.Namespace.Wikipedia))
        {
            if (!taskPage.StartsWith(RfdTitlePrefix))
                continue;

            var rfdDate = taskPage[RfdTitlePrefix.Length..];
            if (rfdDate.Contains('/'))
                continue;

            executor.Run(taskPage, taskTemplate =>
            {
                var title = taskTemplate.Title;

                var page = wiki.GetLastRevision(title);
                if (page == null)
                    return;

                var talkPage = wiki.GetAssociatePageTitle(title)[title];
                if (!talkPage.Namespace.IsTalk())
                    return;

                // remove {{К удалению}}

                if (TemplateTaskUtils.RemoveForDeletionTemplate(parser, page.Text, out var newPageText))
                    wiki.Edit(title, newPageText, Summary, revId: page.Id);

                // add {{Оставлено}}

                var talk = wiki.GetLastRevision(talkPage.Title);
                var parsedTalk = parser.FindTemplates(talk?.Text ?? "", KeptTemplateName);
                var keptTemplate = parsedTalk.FirstOrDefault();

                if (keptTemplate == null)
                {
                    keptTemplate = new() { Name = KeptTemplateName };
                    UpdateKeptTempate(keptTemplate, rfdDate, title);

                    wiki.Edit(talkPage.Title, keptTemplate.ToString() + "\n", Summary, false, revId: talk?.Id);
                }
                else
                {
                    UpdateKeptTempate(keptTemplate, rfdDate, title);

                    parsedTalk.Update(keptTemplate, keptTemplate.ToString());
                    wiki.Edit(talkPage.Title, parsedTalk.Text, Summary, revId: talk!.Id);
                }
            });
        }
    }


    private static void UpdateKeptTempate(Template template, string date, string title)
    {
        var items = template.Args
            .Where(a => a.Name == null)
            .Select((a, i) => new { Date = a.Value, Title = template.Args.FirstOrDefault(x => x.Name == GetArgName(i))?.Value })
            .ToList();

        var newItem = new { Date = date, Title = (string?)title };

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
