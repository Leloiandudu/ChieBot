using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChieBot
{
    public static class Utils
    {
        public static DateTimeFormatInfo DateTimeFormat { get; } = CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat;

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

        public static string Replace(this Match match, string on, string with)
        {
            return on.Substring(0, match.Index) + with + on.Substring(match.Index + match.Length);
        }

        public static IEnumerable<T[]> Partition<T>(this IEnumerable<T> items, int count)
        {
            var i = 0;
            T[] array = null;

            foreach (var item in items)
            {
                if (i == 0)
                    array = new T[count];

                array[i++] = item;

                if (i == count)
                {
                    yield return array;
                    i = 0;
                }
            }

            if (i > 0)
            {
                if (i < count)
                    yield return array.Take(i).ToArray();
                else
                    yield return array;
            }
        }

        public static bool Contains(this IEnumerable<TextRegion> regions, int offset)
        {
            return regions.Any(r => r.Contains(offset));
        }

        public static string Remove(this string text, IEnumerable<TextRegion> regions)
        {
            foreach (var x in regions.OrderByDescending(x => x.Offset).ToArray())
                text = text.Remove(x.Offset, x.Length);
            return text;
        }

        public static DateTimeOffset WithTimeZone(this DateTime dt, TimeZoneInfo tz)
        {
            if (dt.Kind != DateTimeKind.Unspecified)
                throw new Exception("DateTime should be without timezone info");

            dt = TimeZoneInfo.ConvertTimeToUtc(dt, tz);
            var dto = new DateTimeOffset(dt, TimeSpan.Zero);
            return TimeZoneInfo.ConvertTime(dto, tz);
        }

        public static DateOnly ToDateOnly(this DateTimeOffset dto)
        {
            return DateOnly.FromDateTime(dto.Date);
        }

        public static bool IsTalk(this MediaWiki.Namespace ns)
            => ((int)ns) % 2 == 1;
    }
}
