using System;
using System.Globalization;

namespace ChieBot.DYK
{
    static class DYKUtils
    {
        public static bool TryParseIssueDate(string text, out DateTime date)
        {
            if (!DateTime.TryParseExact(text, "d MMMM", Utils.DateTimeFormat, DateTimeStyles.None, out date))
                return false;
            if ((DateTime.Now - date).TotalDays > 30) // нin case of announces for next year
                date = date.AddYears(1);
            return true;
        }
    }
}
