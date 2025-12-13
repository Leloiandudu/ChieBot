using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ChieBot.TemplateTasks;

partial class TemplateBasedTaskExecutor<TTaskTemplate> where TTaskTemplate : TaskTemplateBase
{
    private const string ClosingSectionName = "Итог";
    private static readonly string[] IncludeGroups = ["sysop", "closer"];
    private static readonly string[] ExcludeGroups = ["bot"];

    private readonly Dictionary<string, bool> _powerUsers = [];
    private readonly IMediaWiki _wiki;
    private readonly string _summary;
    private readonly Func<Template, PartiallyParsedWikiText<Template>, TTaskTemplate> _parseTemplate;
    private readonly ParserUtils _parserUtils;
    private readonly Lazy<string[]> _allTemplateNames;

    public TemplateBasedTaskExecutor(IMediaWiki wiki, string templateName, string summary, Func<Template, PartiallyParsedWikiText<Template>, TTaskTemplate> parseTemplate)
    {
        _wiki = wiki;
        _summary = summary;
        _parseTemplate = parseTemplate;
        _parserUtils = new(wiki);
        _allTemplateNames = new(() => _parserUtils.GetAllTemplateNames(templateName));
    }

    public TemplateBasedTaskExecutor(IMediaWiki wiki, string templateName, string summary, Func<Template, TTaskTemplate> parseTemplate)
        : this(wiki, templateName, summary, (tt, page) => parseTemplate(tt))
    {
    }

    public void Run(string title, Action<TTaskTemplate> executeTask)
    {
        var history = Revision.FromHistory(_wiki.GetHistory(title, DateTimeOffset.MinValue));
        LoadUsers(history);

        var page = _parserUtils.FindTemplates(history.First().GetText(_wiki), _allTemplateNames.Value);
        foreach (var template in page.ToArray())
        {
            var tt = TryParse(template, page);
            if (tt == null)
            {
                page.Update(template, $"<span style='color: red'>Ошибка в шаблоне <nowiki>{template}</nowiki>: '''неверный формат аргументов'''</span>");
                continue;
            }

            if (tt.IsDone)
                continue;

            var section = ParserUtils.GetSectionName(page, template);
            if (section != ClosingSectionName)
            {
                page.Update(template, $"<span style='color: red'>Шаблон <nowiki>{template}</nowiki> должен находиться в секции '''Итоги'''.</span>");
                continue;
            }

            var user = GetUser(tt.Title, history, page);
            if (!_powerUsers[user])
            {
                page.Update(template, $"<span style='color: red'>Шаблон <nowiki>{template}</nowiki> установлен пользователем {{{{u|{user}}}}}, не имеющим флага ПИ/А.</span>");
                continue;
            }

            try
            {
                executeTask(tt);
            }
            catch(TempalteTaskException ex)
            {
                page.Update(template, $"<span style='color: red'>Ошибка в шаблоне <nowiki>{template}</nowiki>: '''{ex.Message}'''</span>");
                continue;
            }

            template.Args.Add(new() { Value = TaskTemplateBase.DoneArg });
            page.Update(template, template.ToString());
        }

        if (history.First().GetText(_wiki) != page.Text)
            _wiki.Edit(title, page.Text, _summary);
    }

    private TTaskTemplate? TryParse(Template template, PartiallyParsedWikiText<Template> page)
    {
        try
        {
            return _parseTemplate(template, page);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private void LoadUsers(Revision[] history)
    {
        var users = _wiki.GetUserGroups(history.Select(h => h.Info.User).Distinct().Except(_powerUsers.Keys).ToArray());
        foreach (var (user, groups) in users)
        {
            _powerUsers.Add(user, groups.Any(g => IncludeGroups.Contains(g)) && groups.All(g => !ExcludeGroups.Contains(g)));
        }
    }

    private string GetUser(string title, Revision[] history, PartiallyParsedWikiText<Template> page)
    {
        // looking for the first edit where the template did not exist

        return history.FindEarliest(_wiki, text => _parserUtils.FindTemplates(text, _allTemplateNames.Value)
            .Select(t => TryParse(t, page))
            .Any(t => t?.Title == title)).Info.User;
    }
}

public class TempalteTaskException : Exception
{
    public TempalteTaskException(string message) : base(message) { }
    public TempalteTaskException(string message, Exception inner) : base(message, inner) { }
}
