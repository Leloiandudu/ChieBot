using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace ChieBot
{
    class Program
    {
        static void Main(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback = LetsEncryptWorkaround;

            var moduleArgs = args.SkipWhile(a => a.StartsWith("-")).Skip(1).ToArray();
            var globalArgs = args.Take(args.Length - moduleArgs.Length).ToArray();

            var modules = new Modules.Modules(typeof(Program).Assembly);
            var moduleName = globalArgs.Last();
            var module = modules.Get(moduleName);
            if (module == null)
                throw new Exception($"Module `{moduleName}` not found");

            var wiki = LogIntoWiki();
            wiki.ReadOnly = !globalArgs.Contains("-live");

            module(wiki, moduleArgs);
        }

        private static readonly string[] Chain = new[]
        {
            "A053375BFE84E8B748782C7CEE15827A6AF5A405",
            "933C6DDEE95C9C41A40F9F50493D82BE03AD87BF",
            "DAC9024F54D8F6DF94935FB1732638CA6AD77C13",
        };

        static bool LetsEncryptWorkaround(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
                return false;

            return chain.ChainElements.Cast<X509ChainElement>().Skip(1).Select(c => c.Certificate.GetCertHashString()).SequenceEqual(Chain);
        }

        private static MediaWiki LogIntoWiki()
        {
            var browser = new Browser { UserAgent = GetUserAgent(), Cookies = CookieJar.Load() };
            var wiki = new MediaWiki(new Uri("https://ru.wikipedia.org/w/api.php"), browser);

            if (!wiki.IsLoggedIn())
            {
                var creds = ReadCredentials();
                wiki.Login(creds.Login, creds.Password);
                CookieJar.Save(browser.Cookies);
            }

            return wiki;
        }

        private static string GetUserAgent()
        {
            var ver = typeof(Program).Assembly.GetName().Version;
            var userAgent = $"ChieBot/{ver.Major}.{ver.Minor} (https://bitbucket.org/leloiandudu/chiebot; leloiandudu@gmail.com) .net/4.0";
            if (Mono.Version != null)
                userAgent += $" mono/{Mono.Version}";
            return userAgent;
        }

        private static Credentials ReadCredentials()
        {
            var creds = File.ReadAllLines(Path.Combine(Utils.GetProgramDir(), "credentials.txt"));
            return new Credentials { Login = creds[0], Password = creds[1] };
        }
    }
}
