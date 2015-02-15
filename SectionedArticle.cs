using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChieBot
{
    /// <summary>
    /// Article divided in 2nd-level sections
    /// </summary>
    abstract class SectionedArticle<TSection> : IEnumerable<TSection>
        where TSection : Section, new()
    {
        private static readonly Regex Heading2 = new Regex(@"^==[^=].*==\s*$\n?", RegexOptions.Compiled | RegexOptions.Multiline);

        private readonly string _prefix;
        protected readonly List<TSection> _sections = new List<TSection>();

        public SectionedArticle(string fullText)
        {
            var matches = Heading2.Matches(fullText);
            if (matches.Count == 0)
            {
                _prefix = fullText;
                return;
            }

            _prefix = fullText.Substring(0, matches[0].Index);

            for (var i = 0; i < matches.Count; i++)
            {
                var section = new TSection();
                var match = matches[i];

                section.Title = fullText.Substring(match.Index, match.Length);

                var index = match.Index + match.Length;
                if (i + 1 < matches.Count)
                    section.Text = fullText.Substring(match.Index + match.Length, matches[i + 1].Index - index);
                else
                    section.Text = fullText.Substring(index);

                if (InitSection(section))
                    _sections.Add(section);
            }
        }

        protected virtual bool InitSection(TSection section)
        {
            return true;
        }

        public string FullText
        {
            get { return _prefix + string.Join("", _sections.Select(s => s.FullText)); }
        }

        public void Add(TSection section)
        {
            _sections.Add(section);
        }

        public bool Remove(TSection section)
        {
            return _sections.Remove(section);
        }

        public IEnumerator<TSection> GetEnumerator()
        {
            return _sections.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class Section
    {
        public string Title { get; set; }
        public string Text { get; set; }

        public string FullText
        {
            get { return Title + Text; }
        }
    }
}
