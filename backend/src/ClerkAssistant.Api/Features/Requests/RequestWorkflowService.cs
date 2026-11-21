using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Requests;

public sealed class RequestWorkflowService(
    AppDbContext db,
    RequestCaseService cases,
    AuditLog audit,
    AppClock clock,
    IOptions<OrganizationOptions> organization)
{
    public async Task<RequestDetail> ChangeStatusAsync(Guid id, ChangeStatusRequest body, CancellationToken ct)
    {
        var request = await db.FindRequestAsync(id, ct);
        var today = clock.Today;
        DateOnly? respondedOn = body.Status == RequestStatus.ResponseSent
            ? body.RespondedOn ?? request.RespondedOn ?? today
            : null;
        var errors = new ValidationErrors();
        if (body.Status == RequestStatus.Extended && request.ExtendedDueOn is null)
        {
            errors.Add("status", "Aby oznaczyć sprawę jako przedłużoną, użyj opcji przedłużenia terminu i podaj powód opóźnienia.");
        }

        if (respondedOn > today)
        {
            errors.Add("respondedOn", "Data wysłania odpowiedzi nie może być późniejsza niż dzisiejsza.");
        }
        else if (respondedOn < request.ReceivedOn)
        {
            errors.Add("respondedOn", "Data wysłania odpowiedzi nie może być wcześniejsza niż data wpływu wniosku.");
        }

        errors.ThrowIfAny();

        db.ExpectVersion(request, body.Version, clock.Now);
        var details = DescribeStatusChange(request, body.Status, respondedOn);
        request.Status = body.Status;
        request.RespondedOn = respondedOn;
        if (details is not null)
        {
            audit.Record("request.status-changed", nameof(InfoRequest), id, details);
        }

        await db.SaveChangesAsync(ct);
        return await cases.GetAsync(id, ct);
    }

    public async Task<ExtensionResult> ExtendAsync(Guid id, ExtendDeadlineRequest body, CancellationToken ct)
    {
        var request = await db.FindRequestAsync(id, ct);
        if (request.IsClosed)
        {
            throw ValidationException.For("days", "Nie można przedłużyć terminu w sprawie zakończonej – odpowiedź została już wysłana.");
        }

        var extendedDueOn = Deadlines.ExtendedDueDate(request.DueOn, body.Days);
        var latest = Deadlines.LatestExtendedDueDate(request.ReceivedOn);
        if (extendedDueOn > latest)
        {
            throw ValidationException.For("days",
                $"Termin można przedłużyć najpóźniej do {RequestLetters.ShortDate(latest)} r. – nie dłużej niż 2 miesiące " +
                "od dnia złożenia wniosku (art. 13 ust. 2 ustawy o dostępie do informacji publicznej).");
        }

        db.ExpectVersion(request, body.Version, clock.Now);
        request.ExtendedDueOn = extendedDueOn;
        request.ExtensionDays = body.Days;
        request.ExtensionReason = body.Reason.Trim();
        request.ExtensionKind = body.Kind;
        request.Status = RequestStatus.Extended;
        audit.Record("request.extended", nameof(InfoRequest), id, AuditDetails.Extension(body.Kind, extendedDueOn, body.Days));
        await db.SaveChangesAsync(ct);

        var notice = RequestLetters.ExtensionNotice(request, extendedDueOn, organization.Value, clock.Today);
        return RequestMapper.ToExtensionResult(extendedDueOn, notice);
    }

    private static string? DescribeStatusChange(InfoRequest request, RequestStatus status, DateOnly? respondedOn)
    {
        var sentOn = respondedOn is { } date ? $", data wysłania odpowiedzi: {RequestLetters.ShortDate(date)}" : "";
        if (request.Status != status)
        {
            return AuditDetails.StatusChange(request.Status, status) + sentOn;
        }

        return respondedOn is { } newDate && request.RespondedOn != newDate
            ? $"Data wysłania odpowiedzi: {RequestLetters.ShortDate(newDate)}"
            : null;
    }
}
