using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Assistant;
using ClerkAssistant.Api.Features.Platform;
using ClerkAssistant.Api.Tests.Ai;
using ClerkAssistant.Api.Tests.Infrastructure;
using ClerkAssistant.Api.Tests.Register;
using Microsoft.EntityFrameworkCore;
using static ClerkAssistant.Api.Tests.Assistant.AssistantApi;

namespace ClerkAssistant.Api.Tests.Assistant;

public class AssistantConversationTests(AiApiFactory factory) : IClassFixture<AiApiFactory>
{
    [Fact]
    public async Task Without_ai_the_answer_is_a_basic_fallback_listing_screen_problems()
    {
        var client = await factory.ClientAsync();
        var screen = Screen("/wnioski/nowy", "Nowy wniosek",
            [new ScreenFieldError("Data wpływu", "Data wpływu nie może być późniejsza niż dzisiejsza.")],
            ["Nie udało się zapisać wniosku."]);

        var reply = await client.AskAsync("Dlaczego nie mogę zapisać wniosku?", screen);

        Assert.Equal(["conversation", "status", "delta", "done"], reply.Types);
        var done = reply.Done;
        Assert.False(done.Message.AiUsed);
        Assert.False(done.Message.Interrupted);
        Assert.Equal(AssistantRole.Assistant, done.Message.Role);
        Assert.Equal([AssistantChatService.AiDisabledWarning], done.Warnings);
        Assert.Equal(reply.Answer, done.Message.Content);
        Assert.Contains("- **Data wpływu**: Data wpływu nie może być późniejsza niż dzisiejsza.", done.Message.Content);
        Assert.Contains("- Nie udało się zapisać wniosku.", done.Message.Content);
        Assert.Empty(done.Message.Actions);

        var started = reply.Started;
        Assert.Equal("Dlaczego nie mogę zapisać wniosku?", started.Title);
        Assert.Equal(AssistantRole.User, started.UserMessage.Role);
        Assert.Contains("\"conversationId\":", reply.Raw);
        Assert.Contains("\"role\":\"Assistant\"", reply.Raw);
        Assert.Contains("\"aiUsed\":false", reply.Raw);
    }

    [Fact]
    public async Task Fallback_names_case_facts_blockers_and_the_next_step()
    {
        var today = DateOnly.FromDateTime(factory.Clock.Now.Date);
        var id = await factory.AddRequestAsync(new NewRequest("Koszty remontu szkoły", "Proszę o koszty remontu.", today.AddDays(-2)));
        var caseNumber = await factory.QueryAsync(db => db.Requests.Where(r => r.Id == id).Select(r => r.CaseNumber).SingleAsync(Ct));
        var client = await factory.ClientAsync();

        var reply = await client.AskAsync("Co mam teraz zrobić?", Screen($"/wnioski/{id}", "Sprawa"));

        var content = reply.Done.Message.Content;
        Assert.Contains($"Sprawa **{caseNumber}** ([otwórz sprawę](/wnioski/{id}))", content);
        Assert.Contains("- Przedłużenie terminu: możliwe najpóźniej do", content);
        Assert.Contains("Co blokuje kolejny krok:\n- Do sprawy nie przypisano wydziałów.", content);
        Assert.Contains("**Zalecany kolejny krok:** Przypisz wydziały", content);
    }

    [Fact]
    public async Task Conversation_is_saved_listed_continued_and_deleted()
    {
        var client = await factory.ClientAsync();
        var first = await client.AskAsync("Pierwsze pytanie o rejestr " + new string('x', 100));
        var conversationId = first.Started.ConversationId;

        var second = await client.AskAsync("Drugie pytanie", conversationId: conversationId);

        Assert.Equal(conversationId, second.Started.ConversationId);
        Assert.Equal(ConversationService.TitleLength, first.Started.Title.Length);
        var list = await client.GetFromJsonAsync<AssistantConversationSummary[]>("/api/assistant/conversations", ApiClient.Json, Ct);
        Assert.Equal(conversationId, list![0].Id);
        var conversation = await client.ConversationAsync(conversationId);
        Assert.Equal([AssistantRole.User, AssistantRole.Assistant, AssistantRole.User, AssistantRole.Assistant],
            conversation.Messages.Select(m => m.Role));
        Assert.Equal("Drugie pytanie", conversation.Messages[2].Content);

        var deleted = await client.DeleteAsync($"/api/assistant/conversations/{conversationId}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var gone = await client.GetAsync($"/api/assistant/conversations/{conversationId}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.False(await factory.QueryAsync(db => db.AssistantMessages.AnyAsync(m => m.ConversationId == conversationId, Ct)));
    }

    [Fact]
    public async Task Another_user_cannot_see_continue_or_delete_a_conversation()
    {
        var admin = await factory.ClientAsync();
        var reply = await admin.AskAsync("Prywatne pytanie");
        var conversationId = reply.Started.ConversationId;
        var other = await factory.CreateClerkClientAsync(admin, "asystent.inny");

        var get = await other.GetAsync($"/api/assistant/conversations/{conversationId}", Ct);
        var delete = await other.DeleteAsync($"/api/assistant/conversations/{conversationId}", Ct);
        var send = await other.PostMessageAsync("Dopisuję się", conversationId: conversationId);
        var list = await other.GetFromJsonAsync<AssistantConversationSummary[]>("/api/assistant/conversations", ApiClient.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
        Assert.DoesNotContain(list!, c => c.Id == conversationId);
        Assert.Equal(2, (await admin.ConversationAsync(conversationId)).Messages.Length);
    }

    [Theory]
    [InlineData("", "text")]
    [InlineData("   ", "text")]
    [InlineData(null, "text")]
    public async Task Empty_text_is_rejected_before_streaming(string? text, string field)
    {
        var client = await factory.ClientAsync();

        var response = await client.PostAsJsonAsync("/api/assistant/messages",
            new { conversationId = (Guid?)null, text, screen = Screen() }, ApiClient.Json, Ct);

        var errors = await response.ValidationErrorsAsync();
        Assert.Contains(errors, e => e.Key.Equals(field, StringComparison.OrdinalIgnoreCase) && e.Value.Contains("Wpisz treść wiadomości."));
    }

    [Fact]
    public async Task Oversized_text_and_screen_data_are_rejected_before_streaming()
    {
        var client = await factory.ClientAsync();
        var conversations = await factory.QueryAsync(db => db.AssistantConversations.CountAsync(Ct));

        var tooLong = await client.PostMessageAsync(new string('a', AssistantLimits.MaxTextLength + 1));
        var longAlert = await client.PostMessageAsync("Pytanie", Screen(alerts: [new string('b', AssistantLimits.MaxAlertLength + 1)]));
        var tooManyErrors = await client.PostMessageAsync("Pytanie",
            Screen(fieldErrors: Enumerable.Range(0, AssistantLimits.MaxFieldErrors + 1).Select(i => new ScreenFieldError($"Pole {i}", "Błąd")).ToArray()));

        Assert.Contains("4000", Assert.Single((await tooLong.ValidationErrorsAsync()).Values.SelectMany(v => v)));
        Assert.Contains("Screen.Alerts", (await longAlert.ValidationErrorsAsync()).Keys);
        Assert.Equal(HttpStatusCode.BadRequest, tooManyErrors.StatusCode);
        Assert.Equal(conversations, await factory.QueryAsync(db => db.AssistantConversations.CountAsync(Ct)));
    }

    [Fact]
    public async Task Unknown_conversation_is_not_found()
    {
        var client = await factory.ClientAsync();

        var response = await client.PostMessageAsync("Pytanie", conversationId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Starting_a_conversation_purges_conversations_past_retention()
    {
        var client = await factory.ClientAsync();
        var old = (await client.AskAsync("Stara rozmowa")).Started.ConversationId;
        var recent = (await client.AskAsync("Niedawna rozmowa")).Started.ConversationId;
        await factory.QueryAsync(db => db.AssistantConversations
            .Where(c => c.Id == old)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, factory.Clock.GetUtcNow().AddDays(-31)), Ct));
        await factory.QueryAsync(db => db.AssistantConversations
            .Where(c => c.Id == recent)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, factory.Clock.GetUtcNow().AddDays(-29)), Ct));

        var listed = await client.GetFromJsonAsync<AssistantConversationSummary[]>("/api/assistant/conversations", ApiClient.Json, Ct);
        Assert.DoesNotContain(listed!, c => c.Id == old);
        await client.AskAsync("Nowa rozmowa");

        Assert.False(await factory.QueryAsync(db => db.AssistantConversations.AnyAsync(c => c.Id == old, Ct)));
        Assert.False(await factory.QueryAsync(db => db.AssistantMessages.AnyAsync(m => m.ConversationId == old, Ct)));
        Assert.True(await factory.QueryAsync(db => db.AssistantConversations.AnyAsync(c => c.Id == recent, Ct)));
    }

    [Fact]
    public async Task System_info_reports_assistant_mode_and_retention()
    {
        var client = await factory.ClientAsync();

        var info = await client.GetFromJsonAsync<SystemInfo>("/api/system/info", ApiClient.Json, Ct);

        Assert.False(info!.Ai.AssistantTools);
        Assert.Equal(30, info.AssistantRetentionDays);
    }
}
