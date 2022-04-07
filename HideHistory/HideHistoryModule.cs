using System;
using System.Collections.Generic;
using System.Linq;

namespace ChieBot.HideHistory
{
    class HideHistoryModule : Modules.IModule
    {
        private const string ListPage = "Участник:Lê Lợi (bot)/He protecc";

        public void Execute(MediaWiki wiki, string[] commandLine)
        {
            var titles = wiki.GetPage(ListPage).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var newTitles = new List<string>(titles.Length);

            foreach (var title in titles)
            {
                if (HideHistory(wiki, title))
                    newTitles.Add(title);
            }

            if (newTitles.Count != titles.Length)
                wiki.Edit(ListPage, string.Join("\n", newTitles), "Автоматическое удаление удаленных статей");
        }

        private bool HideHistory(MediaWiki wiki, string title)
        {
            var history = wiki.GetHistory(title, includeParsedComment: true);
            if (history == null)
                return false;

            var revisions = history
                .Where(x => !x.Suppressed)
                .Select(x => new
                {
                    x.Id,
                    HideUser = !x.UserHidden,
                    HideComment = x.CommentHidden ? false : HideComment(x.ParsedComment),
                })
                .ToArray();

            foreach (var hide in new[] {
                new { User = true, Comment = true },
                new { User = true, Comment = false },
                new { User = false, Comment = true },
            })
            {
                var ids = revisions
                    .Where(r => r.HideComment == hide.Comment && r.HideUser == hide.User)
                    .Select(r => r.Id)
                    .ToArray();

                if (ids.Length > 0)
                    wiki.HideRevisions(ids, hideComment: hide.Comment, hideUser: hide.User);
            }

            return true;
        }

        private bool HideComment(string html)
        {
            html = Uri.UnescapeDataString(html);
            return html.Contains("/wiki/Служебная:Вклад/") || html.Contains("/wiki/Участник:");
        }
    }
}
