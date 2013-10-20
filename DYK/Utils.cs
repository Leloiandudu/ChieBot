using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace ChieBot.DYK
{
    static class Utils
    {
        public static bool TryParseIssueDate(string text, out DateTime date)
        {
            if (!DateTime.TryParseExact(text, "d MMMM", CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out date))
                return false;
            if ((DateTime.Now - date).TotalDays > 30) // на случай анонсов для следующего года
                date = date.AddYears(1);
            return true;
        }
    }
}
