using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Assistant;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Tests.Ai;
using ClerkAssistant.Api.Tests.Infrastructure;
using ClerkAssistant.Api.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using static ClerkAssistant.Api.Tests.Assistant.AssistantApi;

namespace ClerkAssistant.Api.Tests.Assistant;

public class AssistantActionTests(FakeAiFactory factory) : IClassFixture<FakeAiFactory>
{
    private const string ProposalAnswer = "Przygotowałem propozycję — sprawdź ją i zatwierdź.";

    private DateOnly Today => DateOnly.FromDateTime(factory.Clock.Now.Date);

    [Fact]
    public async Task Confirmed_status_proposal_changes_the_case_and_is_audited()
    {
        var (id, caseNumber) = await AddCaseAsync(RequestStatus.QueriesSent);
        var client = await factory.ClientAsync();

        var reply = await ProposeAsync(client, AssistantProposals.StatusTool,
            new() { ["caseNumber"] = caseNumber, ["status"] = "ResponseSent" });

        Assert.Contains("Przygotowuję propozycję działania…", reply.Statuses);
        Assert.Contains("zatwierdzi na karcie propozycji", Assert.Single(factory.Chat.StreamCalls.Last().ToolResults));
        var action = Assert.Single(reply.Done.Message.Actions);
        Assert.Equal(AssistantActionKind.ChangeStatus, action.Kind);
        Assert.Equal(AssistantActionStatus.Proposed, action.Status);
        Assert.Equal((id, caseNumber), (action.RequestId, action.CaseNumber));
        Assert.Equal($"Zmiana statusu sprawy {caseNumber} na „Odpowiedź wysłana”", action.Summary);
        Assert.Contains("Data wysłania odpowiedzi: 18.11.2026", action.Details);
        Assert.Equal(RequestStatus.QueriesSent, await StatusAsync(id));

        var confirmed = await DecideAsync<AssistantActionDto>(client, action.Id, "confirm");

        Assert.Equal(AssistantActionStatus.Executed, confirmed.Status);
        Assert.Equal("Zmieniono status sprawy na „Odpowiedź wysłana”. Data wysłania odpowiedzi: 18.11.2026.", confirmed.Outcome);
        Assert.Equal(RequestStatus.ResponseSent, await StatusAsync(id));
        var audit = await factory.QueryAsync(db => db.AuditEntries.Where(a => a.EntityId == id).ToListAsync(Ct));
        Assert.Contains(audit, a => a.Action == "request.status-changed");
        Assert.Contains(audit, a => a.Action == "assistant.action-confirmed" && a.Details == action.Summary);
        var saved = (await client.ConversationAsync(reply.Started.ConversationId)).Messages.Last().Actions.Single();
        Assert.Equal(AssistantActionStatus.Executed, saved.Status);
    }

    [Fact]
    public async Task Proposal_for_a_case_that_changed_meanwhile_fails_without_changing_it()
    {
        var (id, caseNumber) = await AddCaseAsync(RequestStatus.QueriesSent);
        var client = await factory.ClientAsync();
        var action = Assert.Single((await ProposeAsync(client, AssistantProposals.StatusTool,
            new() { ["caseNumber"] = caseNumber, ["status"] = "ResponsePrepared" })).Done.Message.Actions);
        await client.ChangeStatusAsync(id, RequestStatus.NoAction);

        var result = await DecideAsync<AssistantActionDto>(client, action.Id, "confirm");
        var again = await client.DecideAsync(action.Id, "confirm");

        Assert.Equal(AssistantActionStatus.Failed, result.Status);
        Assert.Equal(AssistantActionService.StaleProposal, result.Outcome);
        Assert.Equal(RequestStatus.NoAction, await StatusAsync(id));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.DoesNotContain(await factory.QueryAsync(db => db.AuditEntries.Where(a => a.EntityId == id).ToListAsync(Ct)),
            a => a.Action == "assistant.action-confirmed");
    }

    [Fact]
    public async Task Concurrent_confirmations_execute_the_action_once()
    {
        var (id, caseNumber) = await AddCaseAsync(RequestStatus.QueriesSent);
        var client = await factory.ClientAsync();
        var action = Assert.Single((await ProposeAsync(client, AssistantProposals.StatusTool,
            new() { ["caseNumber"] = caseNumber, ["status"] = "ResponsePrepared" })).Done.Message.Actions);

        var responses = await Task.WhenAll(client.DecideAsync(action.Id, "confirm"), client.DecideAsync(action.Id, "confirm"));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        Assert.Contains(AssistantActionService.AlreadyDecided,
            await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync(Ct));
        Assert.Equal(RequestStatus.ResponsePrepared, await StatusAsync(id));
        Assert.Single(await factory.QueryAsync(db => db.AuditEntries.Where(a => a.EntityId == id && a.Action == "assistant.action-confirmed").ToListAsync(Ct)));
    }

    [Fact]
    public async Task Rejected_proposal_cannot_be_confirmed_later()
    {
        var (id, caseNumber) = await AddCaseAsync(RequestStatus.QueriesSent);
        var client = await factory.ClientAsync();
        var action = Assert.Single((await ProposeAsync(client, AssistantProposals.StatusTool,
            new() { ["caseNumber"] = caseNumber, ["status"] = "ResponsePrepared" })).Done.Message.Actions);

        var rejected = await DecideAsync<AssistantActionDto>(client, action.Id, "reject");
        var confirm = await client.DecideAsync(action.Id, "confirm");

        Assert.Equal(AssistantActionStatus.Rejected, rejected.Status);
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal(RequestStatus.QueriesSent, await StatusAsync(id));
        Assert.Contains(await factory.QueryAsync(db => db.AuditEntries.Where(a => a.EntityId == id).ToListAsync(Ct)),
            a => a.Action == "assistant.action-rejected" && a.Details == action.Summary);
    }

    [Fact]
    public async Task Another_user_cannot_decide_on_a_proposal()
    {
        var (_, caseNumber) = await AddCaseAsync(RequestStatus.QueriesSent);
        var client = await factory.ClientAsync();
        var action = Assert.Single((await ProposeAsync(client, AssistantProposals.StatusTool,
            new() { ["caseNumber"] = caseNumber, ["status"] = "ResponsePrepared" })).Done.Message.Actions);
        var other = await factory.CreateClerkClientAsync(client, "asystent.obcy");

        var confirm = await other.DecideAsync(action.Id, "confirm");
        var reject = await other.DecideAsync(action.Id, "reject");

        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reject.StatusCode);
        Assert.Equal(AssistantActionStatus.Proposed,
            await factory.QueryAsync(db => db.AssistantActions.Where(a => a.Id == action.Id).Select(a => a.Status).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Confirmed_extension_returns_the_letter_for_the_requester()
    {
        var (id, caseNumber) = await AddCaseAsync(RequestStatus.QueriesSent);
        var client = await factory.ClientAsync();
        var action = Assert.Single((await ProposeAsync(client, AssistantProposals.ExtensionTool, new()
        {
            ["caseNumber"] = caseNumber,
            ["days"] = 30,
            ["reason"] = "konieczność kwerendy w archiwum zakładowym",
        })).Done.Message.Actions);

        var confirmed = await DecideAsync<AssistantActionDto>(client, action.Id, "confirm");

        Assert.Equal(AssistantActionKind.ExtendDeadline, action.Kind);
        Assert.Contains("Powód: konieczność kwerendy w archiwum zakładowym", action.Details);
        Assert.Equal(AssistantActionStatus.Executed, confirmed.Status);
        Assert.StartsWith("Przedłużono termin odpowiedzi do", confirmed.Outcome);
        Assert.Contains("termin udzielenia odpowiedzi", confirmed.Letter);
        Assert.Equal(RequestStatus.Extended, await StatusAsync(id));
    }

    [Fact]
    public async Task Invalid_proposal_is_explained_to_the_model_and_not_offered()
    {
        var (_, caseNumber) = await AddCaseAsync(RequestStatus.QueriesSent);
        var client = await factory.ClientAsync();

        var reply = await ProposeAsync(client, AssistantProposals.ExtensionTool,
            new() { ["caseNumber"] = caseNumber, ["days"] = 90, ["reason"] = "kwerenda" });

        Assert.Empty(reply.Done.Message.Actions);
        var result = Assert.Single(factory.Chat.StreamCalls.Last().ToolResults);
        Assert.StartsWith("Nie można przygotować tej propozycji: Termin można przedłużyć najpóźniej do", result);
    }

    [Fact]
    public async Task Department_proposal_shows_the_change_and_fails_when_departments_changed_meanwhile()
    {
        var (id, caseNumber) = await AddCaseAsync(RequestStatus.QueriesSent);
        var client = await factory.ClientAsync();
        var added = Assert.Single((await ProposeAsync(client, AssistantProposals.DepartmentsTool,
            new() { ["caseNumber"] = caseNumber, ["add"] = new[] { "wgk" } })).Done.Message.Actions);
        var stale = Assert.Single((await ProposeAsync(client, AssistantProposals.DepartmentsTool,
            new() { ["caseNumber"] = caseNumber, ["remove"] = new[] { "BKB" } })).Done.Message.Actions);

        var confirmed = await DecideAsync<AssistantActionDto>(client, added.Id, "confirm");
        var failed = await DecideAsync<AssistantActionDto>(client, stale.Id, "confirm");

        Assert.Equal($"Zmiana wydziałów w sprawie {caseNumber}: +WGK", added.Summary);
        Assert.Contains("Dodanie: WGK — Wydział Zieleni i Gospodarki Komunalnej", added.Details);
        Assert.Contains("Wydziały po zmianie: BKB, WGK", added.Details);
        Assert.Equal("Zapisano wydziały sprawy: BKB, WGK.", confirmed.Outcome);
        Assert.Equal(AssistantActionStatus.Failed, failed.Status);
        Assert.Equal(AssistantActionService.StaleProposal, failed.Outcome);
        var symbols = await factory.QueryAsync(db => db.RequestDepartments.Where(l => l.RequestId == id)
            .Select(l => l.Department.Symbol).OrderBy(s => s).ToListAsync(Ct));
        Assert.Equal(["BKB", "WGK"], symbols);
    }

    [Fact]
    public async Task Department_dates_proposal_records_the_query_and_moves_the_case_forward()
    {
        var (id, caseNumber) = await AddCaseAsync(RequestStatus.NoAction);
        var client = await factory.ClientAsync();
        var action = Assert.Single((await ProposeAsync(client, AssistantProposals.DepartmentDatesTool,
            new() { ["caseNumber"] = caseNumber, ["departmentSymbol"] = "bkb", ["askedOn"] = "2026-11-18" })).Done.Message.Actions);

        var confirmed = await DecideAsync<AssistantActionDto>(client, action.Id, "confirm");

        Assert.Contains("Data wysłania zapytania: 18.11.2026 (było: —)", action.Details);
        Assert.Contains("Status sprawy zmieni się na „Zapytania wysłane do wydziałów”.", action.Details);
        Assert.Equal("Zapisano daty dla wydziału BKB: zapytanie 18.11.2026, odpowiedź —.", confirmed.Outcome);
        Assert.Equal(RequestStatus.QueriesSent, await StatusAsync(id));
    }

    private async Task<(Guid Id, string CaseNumber)> AddCaseAsync(RequestStatus status)
    {
        var receivedOn = Today.AddDays(-3);
        var id = await factory.AddRequestAsync(new NewRequest("Wykaz umów na odśnieżanie", "Proszę o wykaz umów.", receivedOn)
        {
            Status = status,
            Departments = [new Assignment("BKB", status == RequestStatus.NoAction ? null : receivedOn)],
        });
        var caseNumber = await factory.QueryAsync(db => db.Requests.Where(r => r.Id == id).Select(r => r.CaseNumber).SingleAsync(Ct));
        return (id, caseNumber);
    }

    private async Task<AssistantReply> ProposeAsync(HttpClient client, string tool, Dictionary<string, object?> arguments)
    {
        factory.Chat.Streamer = (messages, _) => messages.Any(m => m.Role == ChatRole.Tool)
            ? [FakeChatClient.Text(ProposalAnswer)]
            : [FakeChatClient.ToolCall(tool, arguments)];
        var reply = await client.AskAsync("Przygotuj to działanie.");
        Assert.Equal(ProposalAnswer, reply.Done.Message.Content);
        return reply;
    }

    private static async Task<T> DecideAsync<T>(HttpClient client, Guid actionId, string decision)
    {
        var response = await client.DecideAsync(actionId, decision);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(ApiClient.Json, Ct))!;
    }

    private Task<RequestStatus> StatusAsync(Guid id) =>
        factory.QueryAsync(db => db.Requests.Where(r => r.Id == id).Select(r => r.Status).SingleAsync(Ct));
}
