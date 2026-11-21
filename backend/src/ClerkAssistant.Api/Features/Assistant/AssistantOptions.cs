using System.ComponentModel.DataAnnotations;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed class AssistantOptions
{
    public const string Section = "Assistant";

    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;
}
