using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Documents;

public sealed record DocumentMetadata(
    Guid Id,
    DocumentKind Kind,
    string FileName,
    long SizeBytes,
    Guid? DepartmentId,
    string? DepartmentSymbol,
    DateTimeOffset UploadedAt,
    Guid UploadedById,
    bool HasText);

public sealed record DocumentContent(byte[] Content, string ContentType, string FileName);

[Mapper]
public static partial class DocumentMapper
{
    public static partial IQueryable<DocumentMetadata> ProjectToMetadata(this IQueryable<RequestDocument> documents);

    public static partial IQueryable<DocumentText> ProjectToText(this IQueryable<StoredFile> files);

    public static partial IQueryable<DocumentContent> ProjectToContent(this IQueryable<StoredFile> files);

    public static partial DocumentDto ToDto(this DocumentMetadata document, string uploadedBy);

    [MapProperty([nameof(RequestDocument.File), nameof(StoredFile.FileName)], nameof(DocumentMetadata.FileName))]
    [MapProperty([nameof(RequestDocument.File), nameof(StoredFile.SizeBytes)], nameof(DocumentMetadata.SizeBytes))]
    [MapProperty([nameof(RequestDocument.File), nameof(StoredFile.ExtractedText)], nameof(DocumentMetadata.HasText), Use = nameof(HasText))]
    private static partial DocumentMetadata ToMetadata(RequestDocument document);

    [MapProperty(nameof(StoredFile.ExtractedText), nameof(DocumentText.Text))]
    private static partial DocumentText ToText(StoredFile file);

    private static bool HasText(string text) => !string.IsNullOrWhiteSpace(text);
}
