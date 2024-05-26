using System;
using System.Globalization;

namespace ChieBot.DYK
{
    public static class DYKUtils
    {
        public static DateOnly ReferenceDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        /// <summary>
        /// Don't allow dates more than this many days before <see cref="ReferenceDate"/>
        /// </summary>
        public static int MaxDaysInThePast { get; set; } = 30;

        public static bool TryParseIssueDate(string text, out DateOnly date)
        {
            if (!DateOnly.TryParseExact(text, "d MMMM", Utils.DateTimeFormat, DateTimeStyles.None, out date))
                return false;
            date = new(ReferenceDate.Year, date.Month, date.Day);

            if (date < ReferenceDate.AddDays(-MaxDaysInThePast)) // in case of announces for next year
                date = date.AddYears(1);
            return true;
        }
    }
}
