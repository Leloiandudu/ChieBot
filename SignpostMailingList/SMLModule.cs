using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChieBot.SignpostMailingList
{
    class SMLModule : Modules.IModule
    {
        private const string IssuePage = "Проект:Сайнпост-Дайджест";
        private const string SubscribersPage = "Проект:Сайнпост-Дайджест/Подписка";
        private const string Summary = "Автоматическая рассылка";

        private static readonly Regex RefsRegex = new Regex(@"<ref\s*>.*?</ref>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public void Execute(IMediaWiki wiki, string[] commandLine)
        {
            var text = GetMailText(wiki);
            if (text == null)
                return;

            text = "\n\n" + text;
            foreach(var user in GetSubscribers(wiki))
                wiki.Edit(user, text, Summary, true);
        }

        private static string GetMailText(IMediaWiki wiki)
        {
            var text = wiki.GetPage(IssuePage, followRedirects: true);

            var issues = new SectionedArticle<Section>(text, 2);
            var sb = new StringBuilder();
            foreach (var issue in issues)
            {
                var items = GetItems(new SectionedArticle<Section>(issue.Text, 3));
                if (items.Length == 0)
                    continue;

                sb.AppendFormat(@"== '''Сайнпост-дайджест.''' {0} ==

[[Проект:Сайнпост-Дайджест|В этом номере]]:

", StripRefs(GetTitle(issue)));

                foreach (var item in items)
                    sb.AppendFormat("* {0}\n", item);
            }

            if (sb.Length == 0)
                return null;

            sb.AppendLine("\n'''<small>Сообщение оставлено [[У:Lê Lợi (bot)|ботом]]. Чтобы отписаться, удалите эту страницу из [[Проект:Сайнпост-Дайджест/Подписка|списка рассылки]].</small>''' ~~~~~");
            return sb.ToString();
        }

        private IEnumerable<string> GetSubscribers(IMediaWiki wiki)
        {
            var text = wiki.GetPage(SubscribersPage, followRedirects: true);
            var section = new SectionedArticle<Section>(text, 2).Single();

            foreach(var line in section.Text.Split('\n'))
            {
                if (!line.StartsWith("*"))
                    continue;
                yield return ParserUtils.FindAnyLinks(line).Single();
            }
        }

        private static string[] GetItems(IEnumerable<Section> sections)
        {
            var items = sections.Select(s => GetTitle(s)).ToArray();
            if (items.Length > 0)
            {
                if (items[items.Length - 1].Equals("примечания", StringComparison.InvariantCultureIgnoreCase))
                    Array.Resize(ref items, items.Length - 1);
            }
            return items;
        }

        private static string GetTitle(Section item)
        {
            return item.Title.Trim('=', ' ', '\n');
        }

        private static string StripRefs(string text)
        {
            return RefsRegex.Replace(text, "");
        }
    }
}
