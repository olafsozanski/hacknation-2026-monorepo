using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Assistant;

[Mapper]
public static partial class AssistantMapper
{
    public static partial AssistantConversationSummary ToSummary(this AssistantConversation conversation);

    [MapProperty(nameof(AssistantConversation.Messages), nameof(AssistantConversationDto.Messages), Use = nameof(OrderedMessages))]
    public static partial AssistantConversationDto ToDto(this AssistantConversation conversation);

    [MapProperty(nameof(AssistantMessage.Actions), nameof(AssistantMessageDto.Actions), Use = nameof(OrderedActions))]
    public static partial AssistantMessageDto ToDto(this AssistantMessage message);

    public static partial AssistantActionDto ToDto(this AssistantAction action);

    [MapProperty(nameof(AssistantConversation.Id), nameof(AssistantConversationStarted.ConversationId))]
    public static partial AssistantConversationStarted ToStarted(this AssistantConversation conversation, AssistantMessageDto userMessage);

    [MapPropertyFromSource(nameof(AssistantDone.Message))]
    public static partial AssistantDone ToDone(this AssistantMessageDto message, string[] warnings);

    public static partial ChangeStatusRequest ToChangeStatusRequest(this AssistantActionArguments arguments, uint version);

    [MapProperty(nameof(AssistantActionArguments.ExtensionKind), nameof(ExtendDeadlineRequest.Kind))]
    public static partial ExtendDeadlineRequest ToExtendDeadlineRequest(this AssistantActionArguments arguments, uint version);

    public static partial SetDepartmentsRequest ToSetDepartmentsRequest(this AssistantActionArguments arguments);

    public static UpdateRequestDepartmentRequest ToDepartmentDates(DateOnly? askedOn, DateOnly? answeredOn) => new(askedOn, answeredOn);

    public static AssistantText ToText(string text) => new(text);

    public static AssistantError ToError(string message, AssistantMessageDto? assistantMessage) => new(message, assistantMessage);

    private static AssistantMessageDto[] OrderedMessages(List<AssistantMessage> messages) =>
        messages.OrderBy(m => m.Sequence).Select(m => m.ToDto()).ToArray();

    private static AssistantActionDto[] OrderedActions(List<AssistantAction> actions) =>
        actions.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => a.ToDto()).ToArray();
}
