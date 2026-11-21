using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Persistence;

namespace ClerkAssistant.Api.Features.Documents;

public static class DocumentQueries
{
    public static IQueryable<DocumentDto> ToDtos(this IQueryable<RequestDocument> documents, AppDbContext db) =>
        documents
            .OrderBy(d => d.UploadedAt)
            .Select(d => new DocumentDto(
                d.Id,
                d.Kind,
                d.File.FileName,
                d.File.SizeBytes,
                d.DepartmentId,
                d.Department == null ? null : d.Department.Symbol,
                d.UploadedAt,
                db.Users.Where(u => u.Id == d.UploadedById).Select(u => u.FullName).FirstOrDefault() ?? "",
                !string.IsNullOrWhiteSpace(d.File.ExtractedText)));
}
