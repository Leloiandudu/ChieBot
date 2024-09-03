using System;
using System.Collections.Generic;
using System.Linq;
using static ChieBot.MiniWikiParser;

#nullable enable

namespace ChieBot.DYK;

public class IssueParser
{
    private readonly List<string> _errors = [];
    private readonly MiniWikiParser _parser = new();

    public string? Errors => _errors.Count > 0 ? string.Join("\n\n\n", _errors.Select(e => $"* {e}")) : null;

    public IEnumerable<(string Title, string Text, string? Image)> Parse(string issue)
    {
        string? prevImage = null;
        var errors = new List<string>();

        foreach (var line in Split(issue))
        {
            var isItem = line.StartsWith('*');
            var boldLinks = ParserUtils.FindBoldLinks(line);

            if (isItem != (boldLinks.Length > 0))
                errors.Add(isItem ? "Полужирные ссылки не найдены" : "Полужирная ссылка не в элементе списка");

            var expectsImage = line.Contains("{{наилл") || line.Contains("на илл") || line.Contains("на\xa0илл") || line.Contains("на аудиовставке");
            if (expectsImage && !isItem)
                errors.Add("'на илл' не в элементе списка");

            var hasImage = _parser.FileNamespaces.Any(ns => line.Contains(ns, StringComparison.OrdinalIgnoreCase))
                || line.Contains(".jpg") || line.Contains(".png") || line.Contains(".gif");

            if (hasImage && isItem && line.Contains(".svg") && issue.Contains("plainlist"))
                hasImage = false;

            if (hasImage)
            {
                if (prevImage != null)
                    errors.Add($"Две иллюстрации подряд (предыдущая неиспользованная: {Escape(prevImage)})");

                if (!expectsImage)
                    prevImage = line;
                else
                    prevImage = null;

            }
            else if (isItem && expectsImage)
            {
                if (prevImage == null)
                    errors.Add("Иллюстрация не найдена");
            }

            if (isItem)
            {
                if (hasImage)
                    errors.Add("Иллюстрация внутри элемента списка");

                foreach (var link in boldLinks)
                    yield return new(link, line.TrimStart('*', ' '), prevImage);
            }

            if (expectsImage)
                prevImage = null;

            if (errors.Count > 0)
            {
                _errors.Add($"{string.Join("<br>", errors)}<br>{Escape(line)}");
                errors.Clear();
            }
        }

        if (prevImage != null)
            _errors.Add($"Неиспользованное изображение<br>{Escape(prevImage)}");
    }

    private static string Escape(string text) =>
        $"<code><nowiki>{text.Replace("<", "lt;")}</nowiki></code>";

    /// <summary>
    /// Split issue into individual bullet points or anything in between them
    /// </summary>
    /// <param name="issue"></param>
    /// <returns></returns>
    private IEnumerable<string> Split(string issue)
    {
        return _parser
            .Tokenize(issue)

            // remove comments
            .Where(x => x.Type is not (TokenType.Comment or TokenType.NoWiki))

            // split on every new line
            .SplitWhen(x => x.Type == TokenType.NewLine, includeSplitter: false)

            // join adjacent markup back into a string
            .Select(x => string.Join(string.Empty, x.Select(x => x.Text.ToString())))

            // break into items and everything in between
            .SplitWhen(x => x.StartsWith('*'))
            .SelectMany(ExtractItem);

        static IEnumerable<string> ExtractItem(string[] lines)
        {
            var skip = 0;

            // if starts with an item - return that separately
            if (lines[0].StartsWith('*'))
            {
                yield return lines[0];
                skip = 1;
            }

            // combine the rest into newline-separated strings
            if (lines.Length > skip)
                yield return string.Join("\n", lines.Skip(skip));
        }
    }
}
