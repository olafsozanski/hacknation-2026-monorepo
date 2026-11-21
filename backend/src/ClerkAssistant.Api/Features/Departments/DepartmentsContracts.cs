using System.ComponentModel.DataAnnotations;
using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Departments;

public record DepartmentDto(
    Guid Id,
    string Symbol,
    string Name,
    DepartmentKind Kind,
    string Scope,
    string? Email,
    bool IsActive);

public record UpdateDepartmentRequest(
    [property: Required, MaxLength(300)] string Name,
    [property: MaxLength(10_000)] string Scope,
    [property: MaxLength(200), EmailAddress] string? Email,
    bool IsActive);
