﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ChieBot
{
    [DebuggerDisplay("#{Id} {User,nq}")]
    class Revision
    {
        private readonly MediaWiki.RevisionInfo _info;
        private string _text;

        public Revision(MediaWiki.RevisionInfo info)
        {
            _info = info;
        }

        public MediaWiki.RevisionInfo Info => _info;

        public string GetText(MediaWiki wiki)
        {
            if (_text == null)
                _text = wiki.GetPage(_info.Id);
            return _text;
        }

        public static Revision[] FromHistory(MediaWiki.RevisionInfo[] infos)
        {
            return infos.OrderByDescending(r => r.Id).Select(r => new Revision(r)).ToArray();
        }

        public static void LoadText(MediaWiki wiki, IEnumerable<Revision> revisions)
        {
            revisions = revisions.Where(r => r._text == null).ToArray();
            var texts = wiki.GetPages(revisions.Select(r => r.Info.Id).ToArray());
            foreach (var rev in revisions)
                rev._text = texts[rev.Info.Id];
        }
    }

    static class RevisionExtensions
    {
        public static Revision SkipWhile(this IReadOnlyCollection<Revision> history, MediaWiki wiki, Predicate<string> skip)
        {
            var result = history.First();
            foreach (var revBatch in history.Skip(1).Partition(10))
            {
                Revision.LoadText(wiki, revBatch);
                foreach (var rev in revBatch)
                {
                    if (!skip(rev.GetText(wiki)))
                        return result;

                    result = rev;
                }
            }

            return result;
        }
    }
}