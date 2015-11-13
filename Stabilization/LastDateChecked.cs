using System;
using System.Globalization;
using System.IO;

namespace ChieBot.Stabilization
{
    class LastDateChecked
    {
        private readonly string _filename;

        public LastDateChecked(string name)
        {
            _filename = Path.Combine(Utils.GetProgramDir(), name);
        }

        public DateTimeOffset? Get()
        {
            try
            {
                var text = File.ReadAllText(_filename);
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
            File.WriteAllText(_filename, value.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        }
    }
}
