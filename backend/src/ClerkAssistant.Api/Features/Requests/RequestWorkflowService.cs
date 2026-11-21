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
    public const int MaxExtensionReasonLength = 2000;

    public async Task<RequestDetail> ChangeStatusAsync(Guid id, ChangeStatusRequest body, CancellationToken ct)
    {
        var request = await db.FindRequestAsync(id, ct);
        var respondedOn = CheckStatusChange(request, body.Status, body.RespondedOn, clock.Today);

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

    public static DateOnly? CheckStatusChange(InfoRequest request, RequestStatus status, DateOnly? respondedOn, DateOnly today)
    {
        DateOnly? sentOn = status == RequestStatus.ResponseSent
            ? respondedOn ?? request.RespondedOn ?? today
            : null;
        var errors = new ValidationErrors();
        errors.CheckDefined("status", status, RequestRules.UnknownStatus);
        if (status == RequestStatus.Extended && request.ExtendedDueOn is null)
        {
            errors.Add("status", "Aby oznaczyć sprawę jako przedłużoną, użyj opcji przedłużenia terminu i podaj powód opóźnienia.");
        }

        if (sentOn > today)
        {
            errors.Add("respondedOn", "Data wysłania odpowiedzi nie może być późniejsza niż dzisiejsza.");
        }
        else if (sentOn < request.ReceivedOn)
        {
            errors.Add("respondedOn", "Data wysłania odpowiedzi nie może być wcześniejsza niż data wpływu wniosku.");
        }

        errors.ThrowIfAny();
        return sentOn;
    }

    public async Task<ExtensionResult> ExtendAsync(Guid id, ExtendDeadlineRequest body, CancellationToken ct)
    {
        var request = await db.FindRequestAsync(id, ct);
        var extendedDueOn = CheckExtension(request, body);

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

    public static DateOnly CheckExtension(InfoRequest request, ExtendDeadlineRequest body)
    {
        if (!Enum.IsDefined(body.Kind))
        {
            throw ValidationException.For("kind", "Nieprawidłowy rodzaj przedłużenia terminu.");
        }

        if (request.IsClosed)
        {
            throw ValidationException.For("days", "Nie można przedłużyć terminu w sprawie zakończonej – odpowiedź została już wysłana.");
        }

        if (body.Days < 1)
        {
            throw ValidationException.For("days", "Termin można przedłużyć co najmniej o 1 dzień.");
        }

        if (string.IsNullOrWhiteSpace(body.Reason) || body.Reason.Trim().Length > MaxExtensionReasonLength)
        {
            throw ValidationException.For("reason",
                $"Podaj powód przedłużenia terminu (najwyżej {MaxExtensionReasonLength} znaków).");
        }

        var extendedDueOn = Deadlines.ExtendedDueDate(request.DueOn, body.Days);
        var latest = Deadlines.LatestExtendedDueDate(request.ReceivedOn);
        if (extendedDueOn > latest)
        {
            throw ValidationException.For("days",
                $"Termin można przedłużyć najpóźniej do {RequestLetters.ShortDate(latest)} r. – nie dłużej niż 2 miesiące " +
                "od dnia złożenia wniosku (art. 13 ust. 2 ustawy o dostępie do informacji publicznej).");
        }

        return extendedDueOn;
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
