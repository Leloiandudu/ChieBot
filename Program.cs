using System;
using System.Linq;
using System.IO;

namespace ChieBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var moduleArgs = args.SkipWhile(a => a.StartsWith("-")).Skip(1).ToArray();
            var globalArgs = args.Take(args.Length - moduleArgs.Length).ToArray();

            var modules = new Modules.Modules(typeof(Program).Assembly);
            var moduleName = globalArgs.Last();
            var module = modules.Get(moduleName);
            if (module == null)
                throw new Exception($"Module `{moduleName}` not found");

            var ver = typeof(Program).Assembly.GetName().Version;
            var userAgent = $"ChieBot/{ver.Major}.{ver.Minor} (https://bitbucket.org/leloiandudu/chiebot; leloiandudu@gmail.com)"
                + $" .net/4.0";
            if (Mono.Version != null)
                userAgent += $" mono/{Mono.Version}";
            
            var wiki = new MediaWiki(new Uri("https://ru.wikipedia.org/w/api.php"), userAgent);
            wiki.ReadOnly = !globalArgs.Contains("-live");

            module(wiki, moduleArgs, ReadCredentials());
        }

        private static Credentials ReadCredentials()
        {
            var creds = File.ReadAllLines(Path.Combine(Utils.GetProgramDir(), "credentials.txt"));
            return new Credentials { Login = creds[0], Password = creds[1] };
        }
    }
}
