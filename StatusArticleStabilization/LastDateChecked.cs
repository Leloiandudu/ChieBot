using System;
using System.Globalization;
using System.IO;

namespace ChieBot.StatusArticleStabilization
{
    class LastDateChecked
    {
        private static readonly string Filename = Path.Combine(Utils.GetProgramDir(), "sas-lastrev");

        public DateTimeOffset? Get()
        {
            try
            {
                var text = File.ReadAllText(Filename);
                long ticks;
                if (long.TryParse(text, out ticks))
                    return new DateTimeOffset(ticks, TimeSpan.Zero);
            }
            catch(FileNotFoundException)
            {
            }

            return null;
        }

        public void Set(DateTimeOffset value)
        {
            File.WriteAllText(Filename, value.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        }
    }
}
