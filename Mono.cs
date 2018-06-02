using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ChieBot
{
    static class Mono
    {
        private static readonly Regex VersionRegex = new Regex(@"(\d+\.)*\d+");

        static Mono()
        {
            string version;
            try
            {
                version = GetMonoVersion();
            }
            catch (DllNotFoundException)
            {
                return;
            }
            catch (EntryPointNotFoundException)
            {
                return;
            }

            var match = VersionRegex.Match(version);
            if (match.Success)
                Version = Version.Parse(match.Value);
        }

        public static Version Version { get; }

        [DllImport("__Internal", EntryPoint = "mono_get_runtime_build_info")]
        private extern static string GetMonoVersion();
    }
}
