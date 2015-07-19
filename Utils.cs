using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChieBot
{
    static class Utils
    {
        public static DateTimeFormatInfo DateTimeFormat { get; private set; }

        static Utils()
        {
            // mono uses nominative months names only :(
            DateTimeFormat = (DateTimeFormatInfo)CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.Clone();
            DateTimeFormat.MonthNames = new[] { 
                "января", "февраля", "марта", "апреля", "мая", "июня", 
                "июля", "августа", "сентября", "октября", "ноября", "декабря", "" };
        }

        public static TV TryGetValue<TK, TV>(this IDictionary<TK, TV> dic, TK key, TV defaultValue = default(TV))
        {
            TV value;
            if (!dic.TryGetValue(key, out value))
                value = defaultValue;
            return value;
        }

        public static TV GetOrAdd<TK, TV>(this IDictionary<TK, TV> dic, TK key, Func<TV> valueFactory = null)
        {
            TV value;
            if (!dic.TryGetValue(key, out value))
            {
                value = valueFactory == null
                    ? Activator.CreateInstance<TV>()
                    : valueFactory();
                dic.Add(key, value);
            }
            return value;
        }

        public static string GetProgramDir()
        {
            return new System.IO.FileInfo(typeof(Program).Assembly.Location).DirectoryName;
        }

        public static DateTimeOffset ParseDate(string text)
        {
            return DateTimeOffset.Parse(text, Utils.DateTimeFormat, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
    }
}
