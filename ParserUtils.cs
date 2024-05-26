using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot
{
    public class ParserUtils
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

        public static Regex GetArticleTitleRegex(string title)
        {
            var index = title.LastIndexOf(':') + 1;
            return new Regex(@"[\s_]*" + Regex.Escape(title.Substring(0, index)) +  "[" + char.ToUpper(title[index]) + char.ToLower(title[index]) + "]" + string.Join(@"[\s_]+", Regex.Split(title.Substring(index + 1), "[ _]+").Select(Regex.Escape)) + @"[\s_]*");
        }

        private static PartiallyParsedWikiText<Template> FindTemplatesInternal(string text, IEnumerable<string> templateNames, bool skipIgnored)
        {
            var items = new List<Tuple<int, int, Template>>();
            var regex = new Regex(@"\{\{(?:" + string.Join("|", templateNames.Select(t => "(?:" + GetArticleTitleRegex(t) +")")) + @")[|}\s]");
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
                    try
                    {
                        var template = Template.ParseAt(text, match.Index);
                        var item = Tuple.Create(match.Index, template.ToString().Length, template);
                        items.Add(item);
                        i = match.Index + item.Item2;
                    }
                    catch (FormatException)
                    {
                        i = match.Index + 1;
                        continue;
                    }
                }
            }
            return new PartiallyParsedWikiText<Template>(text, items);
        }

        public static PartiallyParsedWikiText<WikiLink> FindLinksTo(string text, string to)
        {
            var regex = new Regex("^:?" + GetArticleTitleRegex(to).ToString() + @"$");
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
            if (atOffset == text.Length)
                return new TextRegion(atOffset, 0);

            var start = atOffset == 0 ? 0 : text.LastIndexOf('\n', atOffset - 1) + 1;
            var end = text.IndexOf('\n', atOffset);
            if (end == -1) end = text.Length - 1;
            return new TextRegion(start, end - start + 1);
        }

        public static TextRegion GetWholeLineAt<T>(PartiallyParsedWikiText<T> text, T atItem)
            where T : class
        {
            return GetWholeLineAt(text.Text, text.GetOffset(atItem));
        }

        public static void SplitTitle(string fullTitle, out string ns, out string title)
        {
            fullTitle = fullTitle.TrimStart(':');
            var index = fullTitle.IndexOf(':');
            ns = fullTitle.Substring(0, index == -1 ? 0 : index);
            title = fullTitle.Substring(index + 1);
        }

        private readonly IMediaWiki _wiki;
        public ParserUtils(IMediaWiki wiki)
        {
            _wiki = wiki;
        }

        private IEnumerable<string> GetAlternativelyNamespacedTitles(string fullName, bool isTempalte)
        {
            string ns, title;
            ParserUtils.SplitTitle(fullName, out ns, out title);

            var namespaces = _wiki.GetNamespaces();
            var nsId = isTempalte
                ? 10
                : namespaces.Single(x => x.Value.Contains(ns)).Key;

            var results = namespaces[nsId].Select(n => string.Format("{0}:{1}", n, title));
            if (isTempalte)
                results = results.Concat(new[] { title });
            return results;
        }

        public PartiallyParsedWikiText<Template> FindTemplates(string text, string templateName, bool skipIgnored = true)
        {
            return FindTemplates(text, new[] { templateName }, skipIgnored);
        }

        public PartiallyParsedWikiText<Template> FindTemplates(string text, string[] templateNames, bool skipIgnored = true)
        {
            var names = templateNames.SelectMany(t => GetAlternativelyNamespacedTitles(t, true)).Distinct().ToArray();
            return FindTemplatesInternal(text, names, skipIgnored);
        }
    }

    [DebuggerDisplay("off: {Offset}, len: {Length}")]
    public class TextRegion
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
