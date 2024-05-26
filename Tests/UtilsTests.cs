using ChieBot;

namespace Tests;

public class UtilsTests
{
    [Theory]
    [InlineData("2010-03-28 1:00", "2010-03-28T01:00:00+03:00")]
    [InlineData("2010-03-28 3:00", "2010-03-28T03:00:00+04:00")]
    [InlineData("2024-05-18 13:00", "2024-05-18T13:00:00+03:00")]
    public void WithTimeZone_Moscow(string date, string expected)
    {
        var dt = DateTime.Parse(date);
        var result = dt.WithTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));
        Assert.Equal(expected, result.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'sszzz"));
    }
}
