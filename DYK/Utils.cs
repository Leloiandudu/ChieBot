using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChieBot.DYK
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

        public static bool TryParseIssueDate(string text, out DateTime date)
        {
            if (!DateTime.TryParseExact(text, "d MMMM", DateTimeFormat, DateTimeStyles.None, out date))
                return false;
            if ((DateTime.Now - date).TotalDays > 30) // нin case of announces for next year
                date = date.AddYears(1);
            return true;

        }
    }
}
