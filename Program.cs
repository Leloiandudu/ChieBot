using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ChieBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var modules = new Modules.Modules(typeof(Program).Assembly);
            var moduleName = args.Last();
            var module = modules.Get(moduleName);
            if (module == null)
                throw new Exception(string.Format("Module `{0}` not found", moduleName));

            var ver = typeof(ChieBot.Program).Assembly.GetName().Version;
            var userAgent = string.Format("ChieBot/{0}.{1} (https://bitbucket.org/leloiandudu/chiebot; leloiandudu@gmail.com)", ver.Major, ver.Minor);
            
            var wiki = new MediaWiki(new Uri("http://ru.wikipedia.org/w/api.php"), userAgent);
            wiki.ReadOnly = !args.Contains("-live");

            module(wiki, args, ReadCredentials());
        }

        private static Credentials ReadCredentials()
        {
            var dir = new FileInfo(typeof(Program).Assembly.Location).DirectoryName;
            var creds = File.ReadAllLines(Path.Combine(dir, "credentials.txt"));
            return new Credentials { Login = creds[0], Password = creds[1] };
        }
    }
}
