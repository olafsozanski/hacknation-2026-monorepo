using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Search;

[Mapper]
internal static partial class SearchHitMapper
{
    [MapValue(nameof(SearchHit.Kind), SearchHitKind.Request)]
    [MapProperty(nameof(RequestHitSource.Subject), nameof(SearchHit.Title))]
    [MapProperty(nameof(RequestHitSource.ReceivedOn), nameof(SearchHit.Date))]
    [MapValue(nameof(SearchHit.KnowledgeKind), null)]
    public static partial SearchHit ToHit(this RequestHitSource source, double score, string snippet);

    [MapValue(nameof(SearchHit.Kind), SearchHitKind.Knowledge)]
    [MapProperty(nameof(KnowledgeHitSource.DocumentId), nameof(SearchHit.Id))]
    [MapValue(nameof(SearchHit.CaseNumber), null)]
    [MapValue(nameof(SearchHit.Status), null)]
    [MapValue(nameof(SearchHit.DepartmentSymbols), Use = nameof(NoDepartments))]
    public static partial SearchHit ToHit(this KnowledgeHitSource source, double score, string snippet, DateOnly date);

    private static string[] NoDepartments() => [];
}
