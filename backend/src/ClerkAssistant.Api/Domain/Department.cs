namespace ClerkAssistant.Api.Domain;

public enum DepartmentKind
{
    Department,
    Office,
}

public class Department
{
    public Guid Id { get; set; }
    public required string Symbol { get; set; }
    public required string Name { get; set; }
    public DepartmentKind Kind { get; set; }
    public string Scope { get; set; } = "";
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
