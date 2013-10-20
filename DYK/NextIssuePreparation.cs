using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ChieBot.DYK
{
    class NextIssuePreparation : IEnumerable<NextIssuePreparation.Item>
    {
        private static readonly Regex MarkedListItems = new Regex(@"^(((\[\[(Файл|File|Изображение|Image))|(\{\{часть изображения\|)).*?\n)?\*[^:*].*?(\n(([*:]{2})|:).*?)*\n", RegexOptions.Compiled | RegexOptions.Multiline);

        private readonly List<Tuple<string, Item>> _items = new List<Tuple<string, Item>>();

        public NextIssuePreparation(string text)
        {
            var index = 0;
            foreach (Match match in MarkedListItems.Matches(text))
            {
                if (match.Index != index)
                {
                    _items.Add(Tuple.Create(text.Substring(index, match.Index - index), (Item)null));
                    index = match.Index;
                }

                _items.Add(Tuple.Create(match.Value, new Item(match.Value)));
                index += match.Length;
            }

            if (index != text.Length)
                _items.Add(Tuple.Create(text.Substring(index), (Item)null));

            Debug.Assert(text == Text);
        }

        public bool Remove(Item item)
        {
            return _items.RemoveAll(x => x.Item2 == item) > 0;
        }

        public string Text
        {
            get { return string.Join("", _items.Select(x => x.Item1)); }
        }

        public class Item
        {
            private static readonly Regex CheckMark = new Regex(@"\{\{злвч\|.*?\|(?<date>\d+ \w+)\}\}", RegexOptions.Compiled);

            public Item(String text)
            {
                Text = text;

                var match = CheckMark.Match(text);
                DateTime date;
                if (match.Success && Utils.TryParseIssueDate(match.Groups["date"].Value, out date))
                    IssueDate = date;
            }

            public string Text { get; private set; }

            public DateTime? IssueDate { get; private set; }
        }

        public IEnumerator<NextIssuePreparation.Item> GetEnumerator()
        {
            return _items.Select(x => x.Item2).Where(x => x != null).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
