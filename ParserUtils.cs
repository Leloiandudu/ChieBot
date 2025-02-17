﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot
{
    public partial class ParserUtils
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
            return GetLinks(BoldLinkRegex.Matches(text).SelectMany(m => LinkRegex.Matches(m.Value)));
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
            return LinkRegex.Matches(text)
                .Select(m => m.Groups["link"].Value)
                .ToArray();
        }

        private static string[] GetLinks(IEnumerable<Match> matches)
        {
            return matches
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

            foreach (Match match in tokens.Matches(wiki))
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
            return new Regex(@"[\s_]*" + Regex.Escape(title.Substring(0, index)) + "[" + char.ToUpper(title[index]) + char.ToLower(title[index]) + "]" + string.Join(@"[\s_]+", Regex.Split(title.Substring(index + 1), "[ _]+").Select(Regex.Escape)) + @"[\s_]*");
        }

        private static PartiallyParsedWikiText<Template> FindTemplatesInternal(string text, IEnumerable<string> templateNames, bool skipIgnored)
        {
            var items = new List<(int, int, Template)>();
            var regex = new Regex(@"\{\{(?:" + string.Join("|", templateNames.Select(t => "(?:" + GetArticleTitleRegex(t) + ")")) + @")[|}\s]");
            var ignored = skipIgnored ? GetIgnoredRegions(text).ToArray() : [];
            for (var i = 0; ;)
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
                        var item = (match.Index, template.ToString().Length, template);
                        items.Add(item);
                        i = match.Index + item.Length;
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
                from match in LinkRegex.Matches(text)
                let link = match.Groups["link"]
                where link.Success && regex.IsMatch(link.Value)
                let title = match.Groups["title"]
                select (match.Index, match.Length, new WikiLink
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
            SplitTitle(fullName, out var ns, out var title);

            var namespaces = _wiki.GetNamespaces();
            var nsId = isTempalte
                ? MediaWiki.Namespace.Template
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

        public static string GetSectionName<T>(PartiallyParsedWikiText<T> page, T item)
            where T : class
        {
            var offset = page.GetOffset(item);
            return HeaderRegex().Matches(page.Text)
                .TakeWhile(m => m.Index < offset)
                .Select(m => m.Groups[1].Value.Trim())
                .LastOrDefault();
        }

        [GeneratedRegex(@"^=+\s*([^=].*?)\s*=+", RegexOptions.Multiline)]
        private static partial Regex HeaderRegex();
    }

    [DebuggerDisplay("off: {Offset}, len: {Length}")]
    public readonly struct TextRegion
    {
        public TextRegion(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        public int Offset { get; }
        public int Length { get; }

        public bool Contains(int offset) =>
            Offset <= offset && offset < Offset + Length;

        public string Get(string str) =>
            str.Substring(Offset, Length);
    }
}
