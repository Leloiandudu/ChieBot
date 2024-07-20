using System.Collections.Generic;
using System;
using System.Linq;

namespace ChieBot.TemplateTasks;

#nullable enable

abstract class TaskTemplateBase
{
    public const string DoneArg = "сделано";

    protected IReadOnlyList<Template.Argument> Args { get; }

    public TaskTemplateBase(Template template, int argCount = 1)
    {
        Args = template.Args;

        if (Args.Count < argCount || Args.Count > argCount + 1 || Args.Any(a => a.Name != null) || Args.Any(a => string.IsNullOrEmpty(a.Value)))
            throw new FormatException();

        if (Args.Count == argCount + 1)
        {
            if (Args[argCount].Value == DoneArg)
                IsDone = true;
            else
                throw new FormatException();
        }
    }

    public string Title => Args[0].Value;

    public bool IsDone { get; }
}

class SimpleTaskTemplate(Template template)
    : TaskTemplateBase(template)
{
}
