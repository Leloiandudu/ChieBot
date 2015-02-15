﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ChieBot.DYK
{
    class NextIssuePreparation : PartiallyParsedWikiText<NextIssuePreparation.Item>
    {
        private static readonly Regex MarkedListItems = new Regex(@"^(((\[\[(Файл|File|Изображение|Image))|(\{\{часть изображения\|)).*?\n)?\*[^:*].*?(\n(([*:]{2})|:).*?)*\n", RegexOptions.Compiled | RegexOptions.Multiline);

        public NextIssuePreparation(string text)
            : base(text, MarkedListItems, x => new Item(x.Value))
        {
        }

        public class Item
        {
            private static readonly Regex CheckMark = new Regex(@"\{\{злвч\|.*?\|(?<date>\d+ \w+)\|*\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            public Item(String text)
            {
                Text = text;

                var match = CheckMark.Match(text);
                DateTime date;
                if (match.Success && DYKUtils.TryParseIssueDate(match.Groups["date"].Value, out date))
                    IssueDate = date;
            }

            public string Text { get; private set; }

            public DateTime? IssueDate { get; private set; }
        }
    }
}
