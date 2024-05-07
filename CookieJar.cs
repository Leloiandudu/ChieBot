using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

#pragma warning disable SYSLIB0011

namespace ChieBot
{
    static class CookieJar
    {
        public static CookieContainer Load()
        {
            try
            {
                using (var stream = File.OpenRead(GetFileName()))
                {
                    var formatter = new BinaryFormatter();
                    return (CookieContainer)formatter.Deserialize(stream);
                }
            }
            catch (FileNotFoundException)
            {
                return new CookieContainer();
            }
        }

        public static void Save(CookieContainer cookies)
        {
            using (var stream = File.OpenWrite(GetFileName()))
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, cookies);
            }
        }

        private static string GetFileName()
        {
            return Path.Combine(Utils.GetProgramDir(), "cookie-jar.bin");
        }
    }
}
