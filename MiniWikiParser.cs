using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot;

#nullable enable

public partial class MiniWikiParser
{
    [DebuggerDisplay("{Type}: {Text}")]
    public record Token(TokenType Type, Range Range, string Source)
    {
        public ReadOnlySpan<char> Text => Source.AsSpan(Range);
    }

    public IEnumerable<Token> Tokenize(string wikiText) =>
        MergeWhitespace(TokenizeInternal(wikiText));

    [GeneratedRegex(@"<!--|-->|(<nowiki)[\s>]|</nowiki>|\[\[|]]|{{|}}|\||\r?(\n)|[\p{Z}\t]+", RegexOptions.Singleline)]
    private static partial Regex Tokens();

    public ICollection<string> FileNamespaces { get; init; } = new List<string> { "файл:", "изображение:", "image:", "file:" };

    private IEnumerable<Token> TokenizeInternal(string wikiText)
    {
        var start = 0;
        var tokenType = TokenType.Text;
        var confirmed = false;
        int? newlineAt = null;
        var nested = 0;

        foreach (Match match in Tokens().Matches(wikiText))
        {
            var token = match.Groups.Cast<Group>().Skip(1).FirstOrDefault(g => g.Success)?.Value ?? match.Value;

            if (tokenType == TokenType.Link && token == "[[")
            {
                if (!confirmed || nested > 1 || !FileNamespaces.Any(ns => wikiText.AsSpan(start + 2).TrimStart(' ').StartsWith(ns, StringComparison.OrdinalIgnoreCase)))
                    tokenType = TokenType.Text;
            }

            if (tokenType == TokenType.Text)
            {
                tokenType = token switch
                {
                    "<!--" => TokenType.Comment,
                    "<nowiki" => TokenType.NoWiki,
                    "[[" => TokenType.Link,
                    "{{" => TokenType.Template,
                    "\n" => TokenType.NewLine,
                    _ => TokenType.Text,
                };

                if (tokenType != TokenType.Text && start != match.Index)
                {
                    yield return GetTextResult(new Range(start, match.Index));
                    start = match.Index;
                    nested = 0;
                }

                if (tokenType == TokenType.Template)
                    newlineAt = null;
            }
            else if (!confirmed)
            {
                if (tokenType == TokenType.Template)
                {
                    if (token == "\n" && newlineAt == null)
                        newlineAt = match.Index;
                    else if (token == "}}" && newlineAt != null)
                        tokenType = TokenType.Text;
                    else if (token == "|")
                        confirmed = newlineAt == null || wikiText.AsSpan(newlineAt.Value..match.Index).IsWhiteSpace();
                }
                else if (tokenType == TokenType.Link)
                {
                    if (token == "\n")
                        tokenType = TokenType.Text;
                    else if (token == "|")
                        confirmed = true;
                }
            }

            if (tokenType == TokenType.Link)
            {
                if (token == "[[")
                    nested++;
                else if (token == "]]")
                    nested--;
            }
            else if (tokenType == TokenType.Template)
            {
                if (token == "{{")
                    nested++;
                else if (token == "}}")
                    nested--;
            }

            if (tokenType == TokenType.NewLine ||
                tokenType == TokenType.Comment && token == "-->" ||
                tokenType == TokenType.NoWiki && token == "</nowiki>" ||
                tokenType == TokenType.Link && token == "]]" && nested == 0 ||
                tokenType == TokenType.Template && token == "}}" && nested == 0)
            {
                yield return new(tokenType, new Range(start, match.Index + match.Length), wikiText);
                start = match.Index + match.Length;
                tokenType = TokenType.Text;
                confirmed = false;
                nested = 0;
            }
        }

        if (start < wikiText.Length)
            yield return GetTextResult(Range.StartAt(start));

        Token GetTextResult(Range range)
        {
            var end = range.End.GetOffset(wikiText.Length);
            var type = TokenType.Whitespace;
            for (var i = range.Start.GetOffset(wikiText.Length); i < end; i++)
            {
                if (!char.IsWhiteSpace(wikiText[i]))
                {
                    type = TokenType.Text;
                    break;
                }
            }

            return new(type, range, wikiText);
        }
    }

    private static IEnumerable<Token> MergeWhitespace(IEnumerable<Token> tokens)
    {
        Token? prev = null;

        foreach (var token in tokens.Append(null))
        {
            if (prev?.Type is TokenType.Text or TokenType.Whitespace &&
                token?.Type is TokenType.Text or TokenType.Whitespace)
            {
                prev = new(new[] { prev.Type, token.Type }.Min(), new(prev.Range.Start, token.Range.End), token.Source);
                continue;
            }

            if (prev != null)
                yield return prev;

            prev = token;
        }
    }

    public enum TokenType
    {
        Text,
        Whitespace,
        NewLine,
        Link,
        Template,
        Comment,
        NoWiki,
    }
}
