namespace ClerkAssistant.Api.Domain;

public enum Urgency
{
    Overdue,
    DueToday,
    DueTomorrow,
    DueSoon,
    OnTrack,
    Closed,
}

public static class Deadlines
{
    public const int StatutoryDays = 14;
    public const int MaxExtensionMonths = 2;
    private const int DueSoonThresholdDays = 3;

    public static DateOnly InitialDueDate(DateOnly receivedOn) =>
        NextWorkingDayOnOrAfter(receivedOn.AddDays(StatutoryDays));

    public static DateOnly LatestExtendedDueDate(DateOnly receivedOn) =>
        NextWorkingDayOnOrAfter(receivedOn.AddMonths(MaxExtensionMonths));

    public static DateOnly ExtendedDueDate(DateOnly dueOn, int extensionDays) =>
        NextWorkingDayOnOrAfter(dueOn.AddDays(extensionDays));

    public static DateOnly NextWorkingDayOnOrAfter(DateOnly date)
    {
        while (PolishHolidays.IsNonWorkingDay(date))
        {
            date = date.AddDays(1);
        }

        return date;
    }

    public static int DaysLeft(DateOnly dueOn, DateOnly today) => dueOn.DayNumber - today.DayNumber;

    public static Urgency UrgencyOf(InfoRequest request, DateOnly today)
    {
        if (request.IsClosed)
        {
            return Urgency.Closed;
        }

        return DaysLeft(request.EffectiveDueOn, today) switch
        {
            < 0 => Urgency.Overdue,
            0 => Urgency.DueToday,
            1 => Urgency.DueTomorrow,
            <= DueSoonThresholdDays => Urgency.DueSoon,
            _ => Urgency.OnTrack,
        };
    }
}
