using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed class AssistantActionService(
    AppDbContext db,
    RequestWorkflowService workflow,
    RequestDepartmentService departments,
    CurrentUser user,
    AuditLog audit,
    AppClock clock)
{
    public const string ActionNotFound = "Nie znaleziono propozycji.";
    public const string AlreadyDecided = "Ta propozycja została już rozpatrzona.";
    public const string StaleProposal = "Sprawa zmieniła się od czasu tej propozycji. Poproś asystenta o nową propozycję.";

    private const string DepartmentNotAssigned = "Wydział nie jest przypisany do tego wniosku.";

    public async Task<AssistantActionDto> ConfirmAsync(Guid id, CancellationToken ct)
    {
        var (action, failure) = await TryExecuteAsync(id, ct);
        if (failure is null)
        {
            return action.ToDto();
        }

        db.ChangeTracker.Clear();
        var failed = await ProposedAsync(id, ct);
        failed.Status = AssistantActionStatus.Failed;
        failed.Outcome = AssistantWording.Clip(failure, 2000);
        failed.DecidedAt = clock.Now;
        await SaveDecisionAsync(ct);
        return failed.ToDto();
    }

    public async Task<AssistantActionDto> RejectAsync(Guid id, CancellationToken ct)
    {
        var action = await ProposedAsync(id, ct);
        action.Status = AssistantActionStatus.Rejected;
        action.DecidedAt = clock.Now;
        audit.Record("assistant.action-rejected", nameof(InfoRequest), action.RequestId, action.Summary);
        await SaveDecisionAsync(ct);
        return action.ToDto();
    }

    private async Task<(AssistantAction Action, string? Failure)> TryExecuteAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var action = await ProposedAsync(id, ct);
        action.Status = AssistantActionStatus.Executed;
        action.DecidedAt = clock.Now;
        await SaveDecisionAsync(ct);
        try
        {
            await EnsureUnchangedAsync(action, ct);
            var (outcome, letter) = await ExecuteAsync(action, ct);
            action.Outcome = outcome;
            action.Letter = letter;
            audit.Record("assistant.action-confirmed", nameof(InfoRequest), action.RequestId, action.Summary);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (action, null);
        }
        catch (Exception ex) when (ex is AppException or DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (action, ex is AppException known ? AssistantWording.Reason(known) : StaleProposal);
        }
    }

    private async Task EnsureUnchangedAsync(AssistantAction action, CancellationToken ct)
    {
        var version = await db.Requests
            .Where(r => r.Id == action.RequestId)
            .Select(r => (uint?)r.Version)
            .FirstOrDefaultAsync(ct);
        if (version != action.RequestVersion)
        {
            throw new ConflictException(StaleProposal);
        }

        if (action.Arguments.PreviousDepartmentIds is { } previous)
        {
            var current = await db.RequestDepartments
                .Where(l => l.RequestId == action.RequestId)
                .Select(l => l.DepartmentId)
                .ToListAsync(ct);
            if (!current.ToHashSet().SetEquals(previous))
            {
                throw new ConflictException(StaleProposal);
            }
        }
    }

    private Task<(string Outcome, string? Letter)> ExecuteAsync(AssistantAction action, CancellationToken ct) => action.Kind switch
    {
        AssistantActionKind.ChangeStatus => ChangeStatusAsync(action, ct),
        AssistantActionKind.ExtendDeadline => ExtendAsync(action, ct),
        AssistantActionKind.SetDepartments => SetDepartmentsAsync(action, ct),
        AssistantActionKind.UpdateDepartmentDates => UpdateDepartmentDatesAsync(action, ct),
        _ => throw new ConflictException("Nieznany rodzaj propozycji."),
    };

    private async Task<(string Outcome, string? Letter)> ChangeStatusAsync(AssistantAction action, CancellationToken ct)
    {
        var request = await workflow.ChangeStatusAsync(
            action.RequestId, action.Arguments.ToChangeStatusRequest(action.RequestVersion), ct);
        var sentOn = request.RespondedOn is { } date ? $" Data wysłania odpowiedzi: {AssistantWording.Date(date)}." : "";
        return ($"Zmieniono status sprawy na {AssistantWording.Quoted(AssistantWording.Status(request.Status))}.{sentOn}", null);
    }

    private async Task<(string Outcome, string? Letter)> ExtendAsync(AssistantAction action, CancellationToken ct)
    {
        var extension = await workflow.ExtendAsync(
            action.RequestId, action.Arguments.ToExtendDeadlineRequest(action.RequestVersion), ct);
        return ($"Przedłużono termin odpowiedzi do {AssistantWording.Date(extension.ExtendedDueOn)}. " +
                "Skopiuj poniższe pismo i wyślij je wnioskodawcy samodzielnie.", extension.NoticeText);
    }

    private async Task<(string Outcome, string? Letter)> SetDepartmentsAsync(AssistantAction action, CancellationToken ct)
    {
        var assigned = await departments.SetAsync(action.RequestId, action.Arguments.ToSetDepartmentsRequest(), ct);
        var symbols = assigned.Length == 0 ? "brak" : string.Join(", ", assigned.Select(d => d.Symbol));
        return ($"Zapisano wydziały sprawy: {symbols}.", null);
    }

    private async Task<(string Outcome, string? Letter)> UpdateDepartmentDatesAsync(AssistantAction action, CancellationToken ct)
    {
        var arguments = action.Arguments;
        var link = await db.RequestDepartments.AsNoTracking()
            .FirstOrDefaultAsync(l => l.RequestId == action.RequestId && l.DepartmentId == arguments.DepartmentId, ct)
            ?? throw new NotFoundException(DepartmentNotAssigned);
        var updated = await departments.UpdateAsync(action.RequestId, link.DepartmentId,
            AssistantMapper.ToDepartmentDates(arguments.AskedOn ?? link.AskedOn, arguments.AnsweredOn ?? link.AnsweredOn), ct);
        return ($"Zapisano daty dla wydziału {updated.Symbol}: zapytanie {AssistantWording.Date(updated.AskedOn)}, " +
                $"odpowiedź {AssistantWording.Date(updated.AnsweredOn)}.", null);
    }

    private async Task<AssistantAction> ProposedAsync(Guid id, CancellationToken ct)
    {
        var userId = user.Id;
        var action = await db.AssistantActions.FirstOrDefaultAsync(a => a.Id == id
                && db.AssistantMessages.Any(m => m.Id == a.MessageId
                    && db.AssistantConversations.Any(c => c.Id == m.ConversationId && c.UserId == userId)), ct)
            ?? throw new NotFoundException(ActionNotFound);
        return action.Status == AssistantActionStatus.Proposed ? action : throw new ConflictException(AlreadyDecided);
    }

    private async Task SaveDecisionAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(AlreadyDecided);
        }
    }
}
