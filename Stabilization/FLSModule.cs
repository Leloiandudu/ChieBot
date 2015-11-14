using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChieBot.Modules;

namespace ChieBot.Stabilization
{
    /// <summary>
    /// Featured list stabilisation
    /// </summary>
    class FLSModule : IModule
    {
        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);
            Stabilize(wiki, "Шаблон:Текущий избранный список");
        }

        private void Stabilize(MediaWiki wiki, string currentTemplateTitle)
        {
            var text = wiki.GetPage(currentTemplateTitle);
            var link = ParserUtils.FindLinks(text).FirstOrDefault();
            if (link == null)
                throw new Exception("Current featured list link not found.");

            wiki.Stabilize(link, "Автоматическая стабилизация избранного списка.", null);
        }
    }
}
