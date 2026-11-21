using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Features.Assistant;
using ClerkAssistant.Api.Tests.Infrastructure;

namespace ClerkAssistant.Api.Tests.Assistant;

public sealed record AssistantReply(string Raw, List<SseEvent> Events)
{
    public AssistantConversationStarted Started => Single("conversation").As<AssistantConversationStarted>();

    public AssistantDone Done => Single("done").As<AssistantDone>();

    public AssistantError Error => Single("error").As<AssistantError>();

    public string Answer => string.Concat(Events.Where(e => e.Type == "delta").Select(e => e.Text));

    public IEnumerable<string> Statuses => Events.Where(e => e.Type == "status").Select(e => e.Text);

    public IEnumerable<string> Types => Events.Select(e => e.Type);

    private SseEvent Single(string type) => Assert.Single(Events, e => e.Type == type);
}

public static class AssistantApi
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static ScreenContext Screen(
        string path = "/",
        string? title = "Rejestr wniosków",
        ScreenFieldError[]? fieldErrors = null,
        string[]? alerts = null) =>
        new(path, title, null, null, fieldErrors ?? [], alerts ?? []);

    public static Task<HttpResponseMessage> PostMessageAsync(
        this HttpClient client, string text, ScreenContext? screen = null, Guid? conversationId = null) =>
        client.PostAsJsonAsync("/api/assistant/messages", new SendAssistantMessage(conversationId, text, screen ?? Screen()),
            ApiClient.Json, Ct);

    public static async Task<AssistantReply> AskAsync(
        this HttpClient client, string text, ScreenContext? screen = null, Guid? conversationId = null)
    {
        var response = await client.PostMessageAsync(text, screen, conversationId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var raw = await response.Content.ReadAsStringAsync(Ct);
        return new AssistantReply(raw, SseReader.Parse(raw));
    }

    public static async Task<AssistantConversationDto> ConversationAsync(this HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<AssistantConversationDto>($"/api/assistant/conversations/{id}", ApiClient.Json, Ct))!;

    public static Task<HttpResponseMessage> DecideAsync(this HttpClient client, Guid actionId, string decision) =>
        client.PostAsync($"/api/assistant/actions/{actionId}/{decision}", null, Ct);
}
