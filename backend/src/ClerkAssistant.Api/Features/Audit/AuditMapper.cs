using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Audit;

[Mapper]
public static partial class AuditMapper
{
    public static partial IQueryable<AuditEntryDto> ProjectToDto(this IQueryable<AuditEntry> entries);
}
