using System.IO;
using System.Net;
using System.Text.Json;

namespace ChieBot
{
    static class CookieJar
    {
        public static CookieContainer Load()
        {
            var cookies = new CookieContainer();

            try
            {
                using var fs = File.OpenRead(GetFileName());
                var collection = JsonSerializer.Deserialize<CookieCollection>(fs);
                cookies.Add(collection);
            }
            catch (FileNotFoundException)
            {
            }

            return cookies;
        }

        public static void Save(CookieContainer cookies)
        {
            using var fs = File.OpenWrite(GetFileName());
            fs.SetLength(0);
            JsonSerializer.Serialize(fs, cookies.GetAllCookies());
        }

        private static string GetFileName()
        {
            return Path.Combine(Utils.GetProgramDir(), "cookie-jar.json");
        }
    }
}
