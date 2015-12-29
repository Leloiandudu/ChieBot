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
    class SectionedArticle<TSection> : List<TSection>
        where TSection : Section, new()
    {
        public int Level { get; private set; }
        public string Prefix { get; set; }

        public SectionedArticle(string fullText, int level = 2)
        {
            Level = level;
            var regex = "^" + new string('=', level) + @"[^=].*" + new string('=', level) + @"\s*$\n?";
            var matches = new Regex(regex, RegexOptions.Multiline).Matches(fullText);
            if (matches.Count == 0)
            {
                Prefix = fullText;
                return;
            }

            Prefix = fullText.Substring(0, matches[0].Index);

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
                    Add(section);
            }
        }

        protected virtual bool InitSection(TSection section)
        {
            return true;
        }

        public string FullText
        {
            get { return Prefix + string.Join(Environment.NewLine, this.Select(s => s.FullText.TrimEnd() + Environment.NewLine)); }
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
