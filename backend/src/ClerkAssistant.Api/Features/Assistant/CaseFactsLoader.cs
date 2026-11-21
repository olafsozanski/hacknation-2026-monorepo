using System.Text.RegularExpressions;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed partial class CaseFactsLoader(AppDbContext db, RequestCaseService cases)
{
    public static Guid? RequestIdFromPath(string path)
    {
        var match = CasePath().Match(path);
        return match.Success && Guid.TryParse(match.Groups["id"].Value, out var id) ? id : null;
    }

    public async Task<CaseFactSheet?> ForRequestAsync(Guid id, CancellationToken ct)
    {
        RequestDetail request;
        try
        {
            request = await cases.GetAsync(id, ct);
        }
        catch (NotFoundException)
        {
            return null;
        }

        var draft = await db.Drafts.Where(d => d.RequestId == id).Select(d => d.Content).FirstOrDefaultAsync(ct);
        return CaseFacts.Describe(new CaseSnapshot(request, draft));
    }

    public async Task<Guid?> FindRequestIdAsync(string? caseNumber, CancellationToken ct)
    {
        var wanted = caseNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(wanted))
        {
            return null;
        }

        var id = await db.Requests.Where(r => r.CaseNumber.ToUpper() == wanted).Select(r => r.Id).FirstOrDefaultAsync(ct);
        return id == Guid.Empty ? null : id;
    }

    [GeneratedRegex(@"^/wnioski/(?<id>[0-9a-fA-F-]{36})(?:/odpowiedz)?/?(?:[?#].*)?$")]
    private static partial Regex CasePath();
}
