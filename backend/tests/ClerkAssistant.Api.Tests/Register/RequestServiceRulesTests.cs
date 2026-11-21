using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;

namespace ClerkAssistant.Api.Tests.Register;

public class RequestServiceRulesTests
{
    [Theory]
    [InlineData("2026-12-28", "2026-12-10", "2026-12-21")]
    [InlineData("2026-11-30", "2026-11-18", "2026-11-25")]
    [InlineData("2026-11-19", "2026-11-18", "2026-11-18")]
    public void Departments_are_asked_to_reply_three_working_days_before_the_deadline(string dueOn, string today, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), RequestDepartmentService.ReplyByDate(DateOnly.Parse(dueOn), DateOnly.Parse(today)));

    [Fact]
    public void Deadline_preview_uses_statutory_rules() =>
        Assert.Equal(
            new DeadlinePreview(new DateOnly(2026, 12, 28), new DateOnly(2027, 2, 10)),
            RequestRegisterService.PreviewDeadline(new DateOnly(2026, 12, 10)));

    [Fact]
    public void Register_summary_counts_open_requests_by_urgency()
    {
        Urgency[] urgencies = [Urgency.Overdue, Urgency.DueToday, Urgency.DueToday, Urgency.DueSoon, Urgency.OnTrack, Urgency.Closed];

        Assert.Equal(new RegisterSummary(1, 2, 0, 1, 5, 1, 6), urgencies.ToRegisterSummary());
    }

    [Fact]
    public void Draft_summary_counts_redactions_by_status()
    {
        var draft = new ResponseDraft
        {
            GeneratedByAi = true,
            Redactions =
            [
                Redaction(RedactionStatus.Suggested),
                Redaction(RedactionStatus.Suggested),
                Redaction(RedactionStatus.Approved),
                Redaction(RedactionStatus.Rejected),
            ],
        };

        var summary = draft.ToSummary();

        Assert.Equal((2, 1, true), (summary.SuggestedRedactions, summary.ApprovedRedactions, summary.GeneratedByAi));
    }

    private static Redaction Redaction(RedactionStatus status) =>
        new(0, 5, "Kowal", RedactionCategory.PersonName, status, RedactionSource.Rule);
}
