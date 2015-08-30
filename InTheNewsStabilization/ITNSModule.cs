﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChieBot.CurrentEventsStabilization
{
    class ITNSModule : Modules.IModule
    {
        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);
            Stabilize(wiki, "Шаблон:Актуальные события", DateTimeOffset.Now.AddDays(7));
        }

        private void Stabilize(MediaWiki wiki, string templateTitle, DateTimeOffset? expiry)
        {
            var links = ParserUtils.FindBoldLinks(wiki.GetPage(templateTitle));
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
