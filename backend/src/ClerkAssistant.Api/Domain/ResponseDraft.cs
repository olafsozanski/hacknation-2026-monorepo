namespace ClerkAssistant.Api.Domain;

public enum RedactionCategory
{
    PersonName,
    Address,
    Pesel,
    IdDocument,
    Phone,
    Email,
    BankAccount,
    Nip,
    DateOfBirth,
    Other,
}

public enum RedactionStatus
{
    Suggested,
    Approved,
    Rejected,
}

public enum RedactionSource
{
    Rule,
    Ai,
    Manual,
}

public record Redaction(
    int Start,
    int Length,
    string Text,
    RedactionCategory Category,
    RedactionStatus Status,
    RedactionSource Source);

public class ResponseDraft
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string Content { get; set; } = "";
    public List<Redaction> Redactions { get; set; } = [];
    public bool GeneratedByAi { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedById { get; set; }
    public uint Version { get; set; }
}
