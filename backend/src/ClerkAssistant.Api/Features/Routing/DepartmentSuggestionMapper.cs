using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Routing;

[Mapper]
public static partial class DepartmentSuggestionMapper
{
    [MapProperty(nameof(Department.Id), nameof(DepartmentSuggestion.DepartmentId))]
    public static partial DepartmentSuggestion ToSuggestion(
        this Department department,
        string reason,
        double confidence,
        int similarCaseCount,
        double? averageResponseDays,
        SuggestionSource source);
}
