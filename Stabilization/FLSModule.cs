using System;
using System.Linq;
using ChieBot.Modules;

namespace ChieBot.Stabilization
{
    /// <summary>
    /// Featured list stabilisation
    /// </summary>
    class FLSModule : IModule
    {
        private const string PageName = "Шаблон:Текущий избранный список";
        private const string TemplateName = "Заглавная/Избранные списки";

        public void Execute(MediaWiki wiki, string[] commandLine)
        {
            var text = wiki.GetPage(PageName);
            var template = new ParserUtils(wiki).FindTemplates(text, TemplateName).FirstOrDefault();
            if (template == null)
                throw new Exception("Current featured list template not found.");

            for (var i = 0; i < 2; i++)
            {
                wiki.Stabilize(template[i].Trim(), "Автоматическая стабилизация избранного списка.", null);
            }

        }
    }
}
