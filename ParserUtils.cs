using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot
{
    static class ParserUtils
    {
        private static readonly Regex LinkRegex = new Regex(@"\[\[:?(?<link>[^|\]]+)(\|(?<title>[^\]]+))?\]\]", RegexOptions.ExplicitCapture);
        private static readonly Regex BoldLinkRegex = new Regex(@"('''[^\[\]']+''')|('''.*?\[\[:?(?<link>[^|\]]+)(\|[^\]]+)?\]\].*?('''|$))|(\[\[:?(?<link>[^|\]]+)\|'''.*?'''\]\])", RegexOptions.ExplicitCapture);
        private static readonly Regex NonArticleLinksRegex = new Regex(@"^(User|У|Участник|User talk|ОУ|Обсуждение участника|ВП|Википедия|File|Image|Файл|Category|Категория|К|Template|Шаблон|Ш)\:", RegexOptions.ExplicitCapture);
        private static readonly Regex LinkHashRegex = new Regex(@"^(?<link>[^#]+)(#.*)?$", RegexOptions.ExplicitCapture);

        /// <summary>
        /// Returns names of all articles from main namespace, linked in bold in the specified <paramref name="text" />.
        /// </summary>
        public static string[] FindBoldLinks(string text)
        {
            return GetLinks(BoldLinkRegex.Matches(text));
        }

        /// <summary>
        /// Returns names of all articles from main namespace, linked in the specified <paramref name="text" />.
        /// </summary>
        public static string[] FindLinks(string text)
        {
            return GetLinks(LinkRegex.Matches(text));
        }

        public static string[] FindAnyLinks(string text)
        {
            return LinkRegex.Matches(text).Cast<Match>()
                .Select(m => m.Groups["link"].Value)
                .ToArray();
        }

        private static string[] GetLinks(MatchCollection matches)
        {
            return matches.Cast<Match>()
                .Select(m => m.Groups["link"])
                .Where(g => g.Success)
                .Select(g => g.Value)
                .Where(l => !NonArticleLinksRegex.IsMatch(l))
                .Select(l => LinkHashRegex.Match(l).Groups["link"].Value)
                .ToArray();
        }

        /// <summary>
        /// Returns commented and nowiki regions.
        /// </summary>
        public static IEnumerable<TextRegion> GetIgnoredRegions(string wiki)
        {
            var tokens = new Regex(string.Join("|", new[] { "<!--", "-->", @"(<nowiki)[\s>]", "</nowiki>" }));
            
            string prevToken = null;
            var start = 0;

            foreach(Match match in tokens.Matches(wiki))
            {
                var token = match.Groups[1].Success ? match.Groups[1].Value : match.Value;
                if ((token == "<!--" || token == "<nowiki") && prevToken == null)
                {
                    prevToken = token;
                    start = match.Index;
                }
                else if (token == "-->" && prevToken == "<!--"
                    || token == "</nowiki>" && prevToken == "<nowiki")
                {
                    yield return new TextRegion(start, match.Index + token.Length - start);
                    prevToken = null;
                }
            }

            if (prevToken != null)
                yield return new TextRegion(start, wiki.Length - start);
        }

        private static bool Contains(this ICollection<TextRegion> regions, int offset)
        {
            return regions.Any(r => r.Contains(offset));
        }

        public static Regex GetArticleTitleRegex(string title)
        {
            return new Regex(@"[\s_]*[" + char.ToUpper(title[0]) + char.ToLower(title[0]) + "]" + string.Join(@"[\s_]+", Regex.Split(title.Substring(1), "[ _]+").Select(Regex.Escape)));
        }

        public static PartiallyParsedWikiText<Template> FindTemplates(string text, string templateName, bool skipIgnored = true)
        {
            var items = new List<Tuple<int, int, Template>>();
            var regex = new Regex(@"\{\{" + GetArticleTitleRegex(templateName).ToString() + @"[|}\s]");
            var ignored = skipIgnored ? new TextRegion[0] : GetIgnoredRegions(text).ToArray();
            for (var i = 0; ; )
            {
                var match = regex.Match(text, i);
                if (!match.Success)
                    break;

                if (ignored.Contains(match.Index))
                {
                    i = match.Index + match.Length;
                }
                else
                {
                    var template = Template.ParseAt(text, match.Index);
                    var item = Tuple.Create(match.Index, template.ToString().Length, template);
                    items.Add(item);
                    i = match.Index + item.Item2;
                }
            }
            return new PartiallyParsedWikiText<Template>(text, items);
        }

        public static PartiallyParsedWikiText<WikiLink> FindLinksTo(string text, string to)
        {
            var regex = new Regex("^" + GetArticleTitleRegex(to).ToString() + @"\s*$");
            return new PartiallyParsedWikiText<WikiLink>(text,
                from Match match in LinkRegex.Matches(text)
                let link = match.Groups["link"]
                where link.Success && regex.IsMatch(link.Value)
                let title = match.Groups["title"]
                select Tuple.Create(match.Index, match.Length, new WikiLink
                {
                    Link = link.Value,
                    Text = title.Success ? title.Value : null,
                })
            );
        }

        public static TextRegion GetWholeLineAt(string text, int atOffset)
        {
            var start = text.LastIndexOf('\n', atOffset);
            var end = text.IndexOf('\n', atOffset);
            return new TextRegion(start, end - start);
        }

        public static TextRegion GetWholeLineAt<T>(PartiallyParsedWikiText<T> text, T atItem)
            where T : class
        {
            return GetWholeLineAt(text.Text, text.GetOffset(atItem));
        }

        public static string Remove(this string text, IEnumerable<TextRegion> regions)
        {
            foreach (var x in regions.OrderByDescending(x => x.Offset).ToArray())
                text = text.Remove(x.Offset, x.Length);
            return text;
        }
    }

    [DebuggerDisplay("off: {Offset}, len: {Length}")]
    class TextRegion
    {
        public TextRegion(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        public int Offset { get; private set; }
        public int Length { get; private set; }

        public bool Contains(int offset)
        {
            return Offset <= offset && offset < Offset + Length;
        }

        public string Get(string str)
        {
            return str.Substring(Offset, Length);
        }
    }
}
