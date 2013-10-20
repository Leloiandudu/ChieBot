using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ChieBot
{
    class PartiallyParsedWikiText<T> : IEnumerable<T>
        where T : class
    {
        private readonly List<Tuple<string, T>> _items = new List<Tuple<string, T>>();

        public PartiallyParsedWikiText(string text, Regex regex, Func<Match, T> itemFactory)
        {
            var index = 0;
            foreach (Match match in regex.Matches(text))
            {
                if (match.Index != index)
                {
                    _items.Add(Tuple.Create(text.Substring(index, match.Index - index), (T)null));
                    index = match.Index;
                }

                _items.Add(Tuple.Create(match.Value, itemFactory(match)));
                index += match.Length;
            }

            if (index != text.Length)
                _items.Add(Tuple.Create(text.Substring(index), (T)null));

            Debug.Assert(text == Text);
        }

        public bool Remove(T item)
        {
            return _items.RemoveAll(x => x.Item2 == item) > 0;
        }

        public string Text
        {
            get { return string.Join("", _items.Select(x => x.Item1)); }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.Select(x => x.Item2).Where(x => x != null).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
