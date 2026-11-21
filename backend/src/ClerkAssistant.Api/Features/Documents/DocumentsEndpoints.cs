using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ClerkAssistant.Api.Features.Documents;

public static class DocumentsEndpoints
{
    public static IServiceCollection AddDocumentsFeature(this IServiceCollection services) =>
        services.AddScoped<DocumentService>();

    public static void MapDocumentsEndpoints(this RouteGroupBuilder api)
    {
        // CSRF is validated by AntiforgeryFilter on the whole /api group (header token, not a form field).
        api.MapPost("/requests/{id:guid}/documents", async (
                Guid id,
                IFormFile? file,
                [FromForm] DocumentKind? kind,
                [FromForm] Guid? departmentId,
                DocumentService documents,
                CancellationToken ct) =>
            {
                var created = await documents.UploadAsync(id, file, kind, departmentId, ct);
                return TypedResults.Created($"/api/documents/{created.Id}/content", created);
            })
            .DisableAntiforgery()
            .RequireRateLimiting(SecurityExtensions.UploadRateLimit);

        var group = api.MapGroup("/documents");
        group.MapGet("/{id:guid}/content", async (Guid id, DocumentService documents, CancellationToken ct) =>
        {
            var file = await documents.DownloadAsync(id, ct);
            return TypedResults.File(file.Content, file.ContentType, file.FileName);
        });
        group.MapGet("/{id:guid}/text", (Guid id, DocumentService documents, CancellationToken ct) => documents.GetTextAsync(id, ct));
        group.MapDelete("/{id:guid}", async (Guid id, DocumentService documents, CancellationToken ct) =>
        {
            await documents.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });
    }
}
