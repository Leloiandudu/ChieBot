using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ChieBot
{
    public class PartiallyParsedWikiText<T> : IEnumerable<T>
        where T : class
    {
        private readonly List<(string Text, T Item)> _items = [];

        public PartiallyParsedWikiText(string text, Regex regex, Func<Match, T> itemFactory)
            : this(text, regex.Matches(text).Select(m => (m.Index, m.Length, itemFactory(m))))
        {
        }

        public PartiallyParsedWikiText(string text, IEnumerable<(int Index, int Length, T Value)> items)
        {
            var index = 0;
            foreach (var match in items)
            {
                if (match.Index != index)
                {
                    _items.Add((text[index..match.Index], null));
                    index = match.Index;
                }

                _items.Add((text.Substring(match.Index, match.Length), match.Value));
                index += match.Length;
            }

            if (index != text.Length)
                _items.Add((text[index..], null));

            Debug.Assert(text == Text);
        }

        public bool Remove(T item)
        {
            return _items.RemoveAll(x => x.Item == item) > 0;
        }

        public int GetOffset(T item)
        {
            var index = _items.FindIndex(x => x.Item == item);
            return _items.Take(index).Sum(x => x.Text.Length);
        }

        public void Update(T item, string text)
        {
            var index = _items.FindIndex(x => x.Item == item);
            _items[index] = (text, item);
        }

        public void InsertAfter(T item, T after)
        {
            var index = _items.FindIndex(x => x.Item == after);
            _items.Insert(index + 1, ("", item));
        }

        public string Text
        {
            get { return string.Join("", _items.Select(x => x.Text)); }
        }

        public IEnumerator<T> GetEnumerator()
            => _items.Select(x => x.Item).Where(x => x != null).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
