using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChieBot.Stabilization
{
    /// <summary>
    /// Current events stabilisation ("In The News")
    /// </summary>
    class ITNSModule : Modules.IModule
    {
        public void Execute(IMediaWiki wiki, string[] commandLine)
        {
            Stabilize(wiki, "Шаблон:Актуальные события", DateTimeOffset.Now.AddDays(7));
        }

        private void Stabilize(IMediaWiki wiki, string templateTitle, DateTimeOffset? expiry)
        {
            var links = ParserUtils.FindLinks(wiki.GetPage(templateTitle));
            var normalized = wiki.Normalize(links);

            foreach(var article in links.Select(x => normalized.TryGetValue(x)).Where(x => x != null).Distinct())
            {
                DateTimeOffset? e;
                if (wiki.GetStabilizationExpiry(article, out e))
                    continue;
                wiki.Stabilize(article, "Автоматическая стабилизация статьи из актуальных событий", expiry);
            }
        }
    }
}
