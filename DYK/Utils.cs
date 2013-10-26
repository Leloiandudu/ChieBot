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
        // mono has incorrect russian months names :(
        private static readonly string[] MonthNames = new[] { 
            "января", "февраля", "марта", "апреля", "мая", "июня", 
            "июля", "августа", "сентября", "октября", "ноября", "декабря" };

        private static readonly Regex DateRegex = new Regex(@"^(\d+) (\w+)$", RegexOptions.Compiled);

        public static bool TryParseIssueDate(string text, out DateTime date)
        {
            date = DateTime.MinValue;

            var match = DateRegex.Match(text);
            if (!match.Success)
                return false;

            var month = Array.IndexOf(MonthNames, match.Groups[2].Value);
            if (month == -1)
                return false;

            date = new DateTime(DateTime.UtcNow.Year, month + 1, int.Parse(match.Groups[1].Value));

            if ((DateTime.Now - date).TotalDays > 30) // in case of announces for next year 
                date = date.AddYears(1);
            return true;
        }
    }
}
