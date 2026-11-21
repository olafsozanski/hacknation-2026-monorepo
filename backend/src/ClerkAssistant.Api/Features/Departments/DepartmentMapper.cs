using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Departments;

[Mapper]
public static partial class DepartmentMapper
{
    public static partial DepartmentDto ToDto(this Department department);
}
