using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.TemplateTasks;
public static partial class TemplateTaskUtils
{
    public const string ForDelTemplateName = "К удалению";

    public static bool RemoveForDeletionTemplate(ParserUtils parser, string text, out string result)
    {
        result = text;

        var parsedPage = parser.FindTemplates(text, ForDelTemplateName);
        foreach (var template in parsedPage.ToArray())
            parsedPage.Update(template, "");

        if (!parsedPage.Any())
            return false;

        result = NoIncludeRegex().Replace(parsedPage.Text, "");
        return true;
    }

    [GeneratedRegex(@"<noinclude></noinclude>\n?")]
    private static partial Regex NoIncludeRegex();
}
