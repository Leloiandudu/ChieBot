using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot;

#nullable enable

public partial class MiniWikiParser
{
    public record Token(TokenType Type, Range Range);

    public IEnumerable<Token> Tokenize(string wikiText) =>
        MergeWhitespace(TokenizeInternal(wikiText));

    [GeneratedRegex(@"<!--|-->|(<nowiki)[\s>]|</nowiki>|\[\[|]]|{{|}}|\||\r?(\n)|\s+", RegexOptions.Singleline)]
    private static partial Regex Tokens();

    private IEnumerable<Token> TokenizeInternal(string wikiText)
    {
        var start = 0;
        var tokenType = TokenType.Text;
        var confirmed = false;

        foreach (Match match in Tokens().Matches(wikiText))
        {
            var token = match.Groups.Cast<Group>().Skip(1).FirstOrDefault(g => g.Success)?.Value ?? match.Value;
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
                }

                if (tokenType != TokenType.NewLine)
                    continue;
            }
            else
            {
                if (tokenType is TokenType.Template or TokenType.Link && !confirmed)
                {
                    if (token == "\n")
                        tokenType = TokenType.Text;
                    else if (token == "|")
                        confirmed = true;
                }
            }
            if (tokenType == TokenType.NewLine ||
            tokenType == TokenType.Comment && token == "-->" ||
            tokenType == TokenType.NoWiki && token == "</nowiki>" ||
            tokenType == TokenType.Link && token == "]]" ||
                tokenType == TokenType.Template && token == "}}")
            {
                yield return new(tokenType, new Range(start, match.Index + match.Length));
                start = match.Index + match.Length;
                tokenType = TokenType.Text;
                confirmed = false;
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

            return new(type, range);
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
                prev = new(new[] { prev.Type, token.Type }.Min(), new(prev.Range.Start, token.Range.End));
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
