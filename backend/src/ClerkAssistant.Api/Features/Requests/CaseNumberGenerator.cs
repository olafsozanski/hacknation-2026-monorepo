using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Requests;

public sealed class CaseNumberGenerator(AppDbContext db, IOptions<OrganizationOptions> organization)
{
    public async Task<(int Sequence, string CaseNumber)> NextAsync(int year, CancellationToken ct)
    {
        var last = await db.Requests.Where(r => r.Year == year).MaxAsync(r => (int?)r.SequenceNumber, ct) ?? 0;
        var sequence = last + 1;
        return (sequence, Format(sequence, year));
    }

    public string Format(int sequence, int year) => $"{organization.Value.CaseNumberPrefix}.{sequence}.{year}";
}
