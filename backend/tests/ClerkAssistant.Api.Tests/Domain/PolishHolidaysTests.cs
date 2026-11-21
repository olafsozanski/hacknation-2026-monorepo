using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Tests.Domain;

public class PolishHolidaysTests
{
    [Theory]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    public void Easter_sunday_is_computed(int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), PolishHolidays.EasterSunday(year));

    [Theory]
    [InlineData("2025-06-19")]
    [InlineData("2026-06-04")]
    [InlineData("2027-05-27")]
    public void Corpus_christi_is_a_public_holiday(string date) =>
        Assert.True(PolishHolidays.IsPublicHoliday(DateOnly.Parse(date)));

    [Theory]
    [InlineData("2026-01-01")]
    [InlineData("2026-01-06")]
    [InlineData("2026-04-06")]
    [InlineData("2026-05-01")]
    [InlineData("2026-08-15")]
    [InlineData("2026-11-11")]
    [InlineData("2026-12-26")]
    public void Statutory_holidays_are_non_working_days(string date) =>
        Assert.True(PolishHolidays.IsNonWorkingDay(DateOnly.Parse(date)));

    [Fact]
    public void Christmas_eve_is_a_holiday_from_2025()
    {
        Assert.True(PolishHolidays.IsPublicHoliday(new DateOnly(2025, 12, 24)));
        Assert.True(PolishHolidays.IsPublicHoliday(new DateOnly(2026, 12, 24)));
        Assert.False(PolishHolidays.IsPublicHoliday(new DateOnly(2024, 12, 24)));
    }

    [Fact]
    public void Weekends_are_non_working_and_ordinary_weekdays_are_working()
    {
        Assert.True(PolishHolidays.IsNonWorkingDay(new DateOnly(2026, 11, 21)));
        Assert.True(PolishHolidays.IsNonWorkingDay(new DateOnly(2026, 11, 22)));
        Assert.False(PolishHolidays.IsNonWorkingDay(new DateOnly(2026, 11, 18)));
        Assert.False(PolishHolidays.IsNonWorkingDay(new DateOnly(2024, 12, 24)));
    }
}
