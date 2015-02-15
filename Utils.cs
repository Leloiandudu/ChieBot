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
    }
}
