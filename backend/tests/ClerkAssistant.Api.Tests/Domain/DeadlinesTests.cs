using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Tests.Domain;

public class DeadlinesTests
{
    private static readonly DateOnly Wednesday = new(2026, 11, 18);

    [Theory]
    [InlineData("2026-11-02", "2026-11-16")]
    [InlineData("2026-11-07", "2026-11-23")]
    [InlineData("2026-10-28", "2026-11-12")]
    [InlineData("2026-12-10", "2026-12-28")]
    [InlineData("2024-12-10", "2024-12-24")]
    public void Initial_due_date_is_14_days_shifted_to_the_next_working_day(string receivedOn, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), Deadlines.InitialDueDate(DateOnly.Parse(receivedOn)));

    [Theory]
    [InlineData("2026-11-02", "2027-01-04")]
    [InlineData("2026-11-16", "2027-01-18")]
    [InlineData("2026-12-31", "2027-03-01")]
    [InlineData("2026-09-15", "2026-11-16")]
    public void Extension_is_capped_at_two_months_from_filing(string receivedOn, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), Deadlines.LatestExtendedDueDate(DateOnly.Parse(receivedOn)));

    [Theory]
    [InlineData("2026-11-30", 30, "2026-12-30")]
    [InlineData("2026-11-30", 26, "2026-12-28")]
    public void Extended_due_date_skips_non_working_days(string dueOn, int days, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), Deadlines.ExtendedDueDate(DateOnly.Parse(dueOn), days));

    [Theory]
    [InlineData("2026-11-17", Urgency.Overdue, -1)]
    [InlineData("2026-11-18", Urgency.DueToday, 0)]
    [InlineData("2026-11-19", Urgency.DueTomorrow, 1)]
    [InlineData("2026-11-21", Urgency.DueSoon, 3)]
    [InlineData("2026-11-22", Urgency.OnTrack, 4)]
    public void Urgency_follows_days_left(string dueOn, Urgency expected, int daysLeft)
    {
        var request = NewRequest(DateOnly.Parse(dueOn), RequestStatus.QueriesSent);

        Assert.Equal(expected, Deadlines.UrgencyOf(request, Wednesday));
        Assert.Equal(daysLeft, Deadlines.DaysLeft(request.EffectiveDueOn, Wednesday));
    }

    [Fact]
    public void Extended_deadline_takes_precedence_and_closed_requests_are_never_urgent()
    {
        var extended = NewRequest(new DateOnly(2026, 11, 10), RequestStatus.Extended);
        extended.ExtendedDueOn = new DateOnly(2026, 12, 10);
        var closed = NewRequest(new DateOnly(2026, 11, 10), RequestStatus.ResponseSent);

        Assert.Equal(Urgency.OnTrack, Deadlines.UrgencyOf(extended, Wednesday));
        Assert.Equal(Urgency.Closed, Deadlines.UrgencyOf(closed, Wednesday));
    }

    private static InfoRequest NewRequest(DateOnly dueOn, RequestStatus status) => new()
    {
        CaseNumber = "BKS.1431.1.2026",
        Subject = "Test",
        DueOn = dueOn,
        Status = status,
    };
}
