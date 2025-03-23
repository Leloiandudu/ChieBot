using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.Archiving
{
    partial class Talk : Section
    {
        public DateTimeOffset? LastActivity { get; private set; }

        public void Parse(IMediaWiki wiki, string[] noSigTemplate)
        {
            LastActivity = ParseSig().Concat(ParseNoSig(wiki, noSigTemplate)).Max();
        }

        private IEnumerable<DateTimeOffset?> ParseSig()
        {
            return FullText
                .Split('\n')
                .SelectMany(comment => DateRegex().Matches(comment).Select(TryParseDate));
        }

        private IEnumerable<DateTimeOffset?> ParseNoSig(IMediaWiki wiki, string[] noSigTemplate)
        {
            return new ParserUtils(wiki)
                .FindTemplates(FullText, noSigTemplate)
                .Select(t =>
                {
                    for (var i = 0; i < t.Args.Count && i < 2; i++)
                    {
                        var arg = t.Args[i].Value;
                        if (NoSigArgRegex().IsMatch(arg))
                            return Utils.ParseDate(arg);
                    }

                    return (DateTimeOffset?)null;
                });
        }

        private static DateTimeOffset? TryParseDate(Match match)
        {
            if (!match.Success)
                return null;
            return Utils.ParseDate(match.Groups[1].Value);
        }

        [GeneratedRegex(@"(\d{1,2}:\d{1,2}, \d+ \w+ \d+) \(UTC\)")]
        private static partial Regex DateRegex();

        // https://ru.wikipedia.org/wiki/Модуль:Unsigned line 11
        [GeneratedRegex(@"[0-9]+ [а-я]+ 20[0-9]+")]
        private static partial Regex NoSigArgRegex();
    }
}
