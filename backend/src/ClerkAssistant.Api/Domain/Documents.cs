namespace ClerkAssistant.Api.Domain;

public enum DocumentKind
{
    Request,
    DepartmentReply,
    Attachment,
}

public class StoredFile
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required byte[] Content { get; set; }
    public required string Sha256 { get; set; }
    public string ExtractedText { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedById { get; set; }
}

public class RequestDocument
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public DocumentKind Kind { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = null!;
    public DateTimeOffset UploadedAt { get; set; }
    public Guid UploadedById { get; set; }
}
