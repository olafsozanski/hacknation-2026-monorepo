namespace ClerkAssistant.Api.Infrastructure.Common;

public sealed class AppClock(TimeProvider time)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public DateTimeOffset Now => time.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).DateTime);
}
