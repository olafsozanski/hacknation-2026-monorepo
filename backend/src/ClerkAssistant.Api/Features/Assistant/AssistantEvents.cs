using System.Net.ServerSentEvents;
using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Assistant;

public static class AssistantEvents
{
    public static SseItem<object> Conversation(AssistantConversation conversation, AssistantMessageDto userMessage) =>
        new(conversation.ToStarted(userMessage), "conversation");

    public static SseItem<object> Status(string text) => new(AssistantMapper.ToText(text), "status");

    public static SseItem<object> Delta(string text) => new(AssistantMapper.ToText(text), "delta");

    public static SseItem<object> Done(AssistantMessageDto message, string[] warnings) => new(message.ToDone(warnings), "done");

    public static SseItem<object> Error(string message, AssistantMessageDto? assistantMessage) =>
        new(AssistantMapper.ToError(message, assistantMessage), "error");
}
