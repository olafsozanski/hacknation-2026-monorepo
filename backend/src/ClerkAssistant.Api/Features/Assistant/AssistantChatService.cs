using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed class AssistantChatService(
    ConversationService conversations,
    AssistantContextBuilder context,
    AssistantLookups lookups,
    AssistantProposals proposals,
    AppGuide guide,
    AiGateway ai)
{
    public const string AiDisabledWarning = "Model AI jest wyłączony — odpowiadam w trybie podstawowym.";
    public const string InterruptedError = "Odpowiedź została przerwana, ponieważ model AI przestał odpowiadać. Spróbuj ponownie.";
    public const string TooLongWarning = "Odpowiedź była zbyt długa i została skrócona.";
    public const string PreparingStatus = "Przygotowuję odpowiedź…";
    public const int MaxAnswerLength = 20_000;

    private const string Operation = "assistant-chat";
    private const string ProposalToolPrefix = "propose_";

    public async Task<IAsyncEnumerable<SseItem<object>>> StartAsync(SendAssistantMessage body, CancellationToken ct)
    {
        CheckScreen(body.Screen);
        var text = body.Text.Trim();
        var conversation = await conversations.OpenAsync(body.ConversationId, text, ct);
        var question = await conversations.AddMessageAsync(
            conversation, new AssistantMessage { Role = AssistantRole.User, Content = text }, ct);
        return StreamAsync(conversation, question, WithoutEmptyEntries(body.Screen), ct);
    }

    private async IAsyncEnumerable<SseItem<object>> StreamAsync(
        AssistantConversation conversation,
        AssistantMessage question,
        ScreenContext screen,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return AssistantEvents.Conversation(conversation, question.ToDto());
        yield return AssistantEvents.Status(PreparingStatus);
        var situation = await context.DescribeAsync(screen, ct);
        if (!ai.IsEnabled)
        {
            var basic = await SaveFallbackAsync(conversation, situation, question.Content, ct);
            yield return AssistantEvents.Delta(basic.Content);
            yield return AssistantEvents.Done(basic, [AiDisabledWarning]);
            yield break;
        }

        var history = await conversations.HistoryAsync(conversation.Id, ct);
        var messages = await context.MessagesAsync(situation, history, question.Content, ct);
        var tools = ai.CanUseTools ? lookups.Functions().Concat(proposals.Functions()).ToList() : null;
        var answer = new StringBuilder();
        var failed = false;
        var truncated = false;
        var saved = false;
        try
        {
            await foreach (var update in ai.StreamChatAsync(Operation, messages, tools, ct))
            {
                if (update.Kind == AiStreamUpdateKind.Failed)
                {
                    failed = true;
                    break;
                }

                if (update.Kind == AiStreamUpdateKind.ToolCall)
                {
                    yield return AssistantEvents.Status(ToolStatus(update.Value));
                    continue;
                }

                answer.Append(update.Value);
                yield return AssistantEvents.Delta(update.Value);
                if (answer.Length >= MaxAnswerLength)
                {
                    truncated = true;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(answer.ToString()))
            {
                var basic = await SaveFallbackAsync(conversation, situation, question.Content, ct);
                saved = true;
                yield return AssistantEvents.Delta(basic.Content);
                yield return AssistantEvents.Done(basic, [AiGateway.FallbackWarning]);
            }
            else if (failed)
            {
                var partial = await SaveAnswerAsync(conversation, answer.ToString(), interrupted: true, [], CancellationToken.None);
                saved = true;
                yield return AssistantEvents.Error(InterruptedError, partial);
            }
            else
            {
                var complete = await SaveAnswerAsync(conversation, answer.ToString(), truncated, proposals.Proposed, ct);
                saved = true;
                yield return AssistantEvents.Done(complete, truncated ? [TooLongWarning] : []);
            }
        }
        finally
        {
            if (!saved && !string.IsNullOrWhiteSpace(answer.ToString()))
            {
                await SaveAnswerAsync(conversation, answer.ToString(), interrupted: true, [], CancellationToken.None);
            }
        }
    }

    private async Task<AssistantMessageDto> SaveFallbackAsync(
        AssistantConversation conversation, AssistantSituation situation, string question, CancellationToken ct)
    {
        var message = new AssistantMessage
        {
            Role = AssistantRole.Assistant,
            Content = AssistantFallback.Answer(situation, guide, question),
        };
        return (await conversations.AddMessageAsync(conversation, message, ct)).ToDto();
    }

    private async Task<AssistantMessageDto> SaveAnswerAsync(
        AssistantConversation conversation, string content, bool interrupted, List<AssistantAction> actions, CancellationToken ct)
    {
        var message = new AssistantMessage
        {
            Role = AssistantRole.Assistant,
            Content = content,
            AiUsed = true,
            Interrupted = interrupted,
            Actions = actions,
        };
        return (await conversations.AddMessageAsync(conversation, message, ct)).ToDto();
    }

    private static string ToolStatus(string tool) => tool switch
    {
        AssistantLookups.GetCaseTool => "Sprawdzam dane sprawy…",
        AssistantLookups.FindRequestsTool => "Szukam w rejestrze…",
        AssistantLookups.SearchArchiveTool => "Przeszukuję archiwum i bazę wiedzy…",
        AssistantLookups.FindDepartmentsTool => "Szukam wydziałów…",
        _ when tool.StartsWith(ProposalToolPrefix, StringComparison.Ordinal) => "Przygotowuję propozycję działania…",
        _ => PreparingStatus,
    };

    private static void CheckScreen(ScreenContext screen)
    {
        var errors = new ValidationErrors();
        if (screen.FieldErrors?.Any(e => e?.Label?.Length > AssistantLimits.MaxScreenText || e?.Message?.Length > AssistantLimits.MaxScreenText) == true)
        {
            errors.Add("Screen.FieldErrors", $"Opis błędu pola może mieć najwyżej {AssistantLimits.MaxScreenText} znaków.");
        }

        if (screen.Alerts?.Any(a => a?.Length > AssistantLimits.MaxAlertLength) == true)
        {
            errors.Add("Screen.Alerts", $"Komunikat z ekranu może mieć najwyżej {AssistantLimits.MaxAlertLength} znaków.");
        }

        errors.ThrowIfAny();
    }

    private static ScreenContext WithoutEmptyEntries(ScreenContext screen) => screen with
    {
        FieldErrors = (screen.FieldErrors ?? []).Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Message)).ToArray(),
        Alerts = (screen.Alerts ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).ToArray(),
    };
}
