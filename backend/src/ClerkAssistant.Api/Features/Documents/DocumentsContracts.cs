using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Documents;

public record DocumentDto(
    Guid Id,
    DocumentKind Kind,
    string FileName,
    long SizeBytes,
    Guid? DepartmentId,
    string? DepartmentSymbol,
    DateTimeOffset UploadedAt,
    string UploadedBy,
    bool HasText);

public record DocumentText(string Text);
