namespace ClerkAssistant.Api.Domain;

public static class PolishHolidays
{
    public static bool IsNonWorkingDay(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || IsPublicHoliday(date);

    public static bool IsPublicHoliday(DateOnly date)
    {
        var easter = EasterSunday(date.Year);
        return (date.Month, date.Day) switch
        {
            (1, 1) or (1, 6) or (5, 1) or (5, 3) or (8, 15) or (11, 1) or (11, 11) or (12, 25) or (12, 26) => true,
            (12, 24) => date.Year >= 2025,
            _ => date == easter
                 || date == easter.AddDays(1)
                 || date == easter.AddDays(49)
                 || date == easter.AddDays(60),
        };
    }

    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
