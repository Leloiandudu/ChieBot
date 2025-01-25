using System;
using System.Linq;
using ChieBot.Modules;
using static ChieBot.MiniWikiParser;

namespace ChieBot.TemplateTasks;

public class MoveToUserPageModule : IModule
{
    public const string CategoryName = "Википедия:К удалению:К переносу в личное пространство";
    public const string TemplateName = "Перенести в личное пространство";
    public const string Summary = "Автоматический перенос статьи.";

    public const string RfdTitlePrefix = "Википедия:К удалению/";
    public const string ForDelTemplateName = "К удалению";

    public void Execute(IMediaWiki wiki, string[] commandLine)
    {
        var parser = new ParserUtils(wiki);
        var executor = new TemplateBasedTaskExecutor<MoveTaskTemplate>(wiki, TemplateName, Summary, t => new MoveTaskTemplate(t));

        foreach (var taskPage in wiki.GetPagesInCategory(CategoryName, MediaWiki.Namespace.Wikipedia))
        {
            if (!taskPage.StartsWith(RfdTitlePrefix))
                continue;

            executor.Run(taskPage, taskTemplate =>
            {
                var page = wiki.GetLastRevision(taskTemplate.Title);
                if (page == null)
                    return;

                if (page.Namespace == MediaWiki.Namespace.User)
                    throw new TempalteTaskException("имя пользователя должно быть вторым параметром");

                if (page.Namespace.IsTalk())
                    throw new TempalteTaskException("перенос обсуждений не поддерживается");

                var user = wiki.GetUsers(taskTemplate.UserName).TryGetValue(taskTemplate.UserName);
                if (user == null)
                    throw new TempalteTaskException("участник не существует");

                // remove {{К удалению}}
                TemplateTaskUtils.RemoveForDeletionTemplate(parser, page.Text, out var newPageText);

                // add {{Временная статья}}
                newPageText = "{{Временная статья}}\n" + newPageText;

                // comment out categories & nav templates
                newPageText = RemoveCategoriesAndNavTemplates(newPageText);

                wiki.Edit(taskTemplate.Title, newPageText, Summary, revId: page.Id);

                wiki.Move(taskTemplate.Title, $"User:{taskTemplate.UserName}/{taskTemplate.Title}", Summary, false);
            });
        }
    }

    private static string RemoveCategoriesAndNavTemplates(string page)
    {
        var lines = new MiniWikiParser().Tokenize(page).SplitWhen(x => x.Type == TokenType.NewLine, includeSplitter: false).ToArray();

        var firstLine = lines
            .Reverse()
            .TakeWhile(line => line.All(t => t.Type is TokenType.Whitespace or TokenType.Template or TokenType.Comment || IsCategory(t)))
            .LastOrDefault();

        if (firstLine == null)
            return page;

        firstLine = lines
            .SkipWhile(x => x != firstLine)
            .FirstOrDefault();

        var startOfFooter = firstLine[0].Range.Start;

        var footer = page[startOfFooter..]
            .Replace("<!--", "<!~~")
            .Replace("-->", "~~>");

        return $"{page[..startOfFooter]}<!--{footer}-->";

        static bool IsCategory(Token token)
        {
            if (token.Type != TokenType.Link)
                return false;

            var name = token.Text[2..].TrimStart();
            return name.StartsWith("К:") || name.StartsWith("Категория:") || name.StartsWith("Category:");
        }
    }

    class MoveTaskTemplate(Template template) : TaskTemplateBase(template, 2)
    {
        public string UserName => Args[1].Value;
    }
}
