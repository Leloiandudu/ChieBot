using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ChieBot.TemplateTasks;

partial class TemplateBasedTaskExecutor(IMediaWiki _wiki, string _templateName, string _summary)
{
    private const string ClosingSectionName = "Итог";
    private static readonly string[] IncludeGroups = ["sysop", "closer"];
    private static readonly string[] ExcludeGroups = ["bot"];
    private const string DoneArg = "сделано";

    private readonly Dictionary<string, bool> _powerUsers = [];
    private readonly Lazy<string[]> _allTemplateNames = new(() => _wiki.GetAllPageNames("Template:" + _templateName));

    private record TaskTemplate(string Title, bool IsDone);

    public void Run(string title, Action<string> executeTask)
    {
        var history = Revision.FromHistory(_wiki.GetHistory(title, DateTimeOffset.MinValue));
        LoadUsers(history);

        var page = new ParserUtils(_wiki).FindTemplates(history.First().GetText(_wiki), _allTemplateNames.Value);
        foreach (var template in page.ToArray())
        {
            var tt = TryParse(template);
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

            var user = GetUser(tt.Title, history);
            if (!_powerUsers[user])
            {
                page.Update(template, $"<span style='color: red'>Шаблон <nowiki>{template}</nowiki> установлен пользователем {{{{u|{user}}}}}, не имеющим флага ПИ/А.</span>");
                continue;
            }

            executeTask(tt.Title);

            template.Args.Add(new() { Value = DoneArg });
            page.Update(template, template.ToString());
        }

        if (history.First().GetText(_wiki) != page.Text)
            _wiki.Edit(title, page.Text, _summary);
    }

    private static TaskTemplate? TryParse(Template template)
    {
        if (template.Args.Count == 0)
            return null;

        if (template.Args[0].Name != null)
            return null;

        if (template.Args.Count == 2 && template.Args[1].Name == null && template.Args[1].Value == DoneArg)
            return new(null!, true);

        if (template.Args.Count == 1 && !string.IsNullOrWhiteSpace(template.Args[0].Value))
            return new(template.Args[0].Value, false);

        return null;
    }

    private void LoadUsers(Revision[] history)
    {
        var users = _wiki.GetUserGroups(history.Select(h => h.Info.User).Distinct().Except(_powerUsers.Keys).ToArray());
        foreach (var (user, groups) in users)
        {
            _powerUsers.Add(user, groups.Any(g => IncludeGroups.Contains(g)) && groups.All(g => !ExcludeGroups.Contains(g)));
        }
    }

    private string GetUser(string title, Revision[] history)
    {
        // looking for the first edit where the template did not exist

        return history.FindEarliest(_wiki, text => new ParserUtils(_wiki).FindTemplates(text, _allTemplateNames.Value)
            .Select(t => TryParse(t))
            .Any(t => t?.Title == title)).Info.User;
    }
}
