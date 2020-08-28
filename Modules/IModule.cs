using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChieBot.Modules
{
    interface IModule
    {
        void Execute(MediaWiki wiki, string[] commandLine);
    }
}
