namespace ClerkAssistant.Api.Features.Requests;

public static class RequestsEndpoints
{
    public static IServiceCollection AddRequestsFeature(this IServiceCollection services) =>
        services
            .AddScoped<CaseNumberGenerator>()
            .AddScoped<RequestRegisterService>()
            .AddScoped<RequestCaseService>()
            .AddScoped<RequestWorkflowService>()
            .AddScoped<RequestDepartmentService>();

    public static void MapRequestsEndpoints(this RouteGroupBuilder api)
    {
        var requests = api.MapGroup("/requests");

        requests.MapGet("/", ([AsParameters] RegisterFilter filter, RequestRegisterService register, CancellationToken ct) =>
            register.ListAsync(filter, ct));
        requests.MapGet("/summary", (int? year, RequestRegisterService register, CancellationToken ct) =>
            register.SummaryAsync(year, ct));
        requests.MapGet("/years", (RequestRegisterService register, CancellationToken ct) => register.YearsAsync(ct));
        requests.MapGet("/deadline-preview", (DateOnly receivedOn) => RequestRegisterService.PreviewDeadline(receivedOn));

        requests.MapGet("/{id:guid}", (Guid id, RequestCaseService cases, CancellationToken ct) => cases.GetAsync(id, ct));
        requests.MapPost("/", async (CreateRequestRequest body, RequestCaseService cases, CancellationToken ct) =>
        {
            var created = await cases.CreateAsync(body, ct);
            return TypedResults.Created($"/api/requests/{created.Id}", created);
        });
        requests.MapPut("/{id:guid}", (Guid id, UpdateRequestRequest body, RequestCaseService cases, CancellationToken ct) =>
            cases.UpdateAsync(id, body, ct));
        requests.MapGet("/{id:guid}/history", (Guid id, RequestCaseService cases, CancellationToken ct) => cases.HistoryAsync(id, ct));

        requests.MapPost("/{id:guid}/status", (Guid id, ChangeStatusRequest body, RequestWorkflowService workflow, CancellationToken ct) =>
            workflow.ChangeStatusAsync(id, body, ct));
        requests.MapPost("/{id:guid}/extension", (Guid id, ExtendDeadlineRequest body, RequestWorkflowService workflow, CancellationToken ct) =>
            workflow.ExtendAsync(id, body, ct));

        requests.MapPut("/{id:guid}/departments", (Guid id, SetDepartmentsRequest body, RequestDepartmentService departments, CancellationToken ct) =>
            departments.SetAsync(id, body, ct));
        requests.MapPatch("/{id:guid}/departments/{departmentId:guid}",
            (Guid id, Guid departmentId, UpdateRequestDepartmentRequest body, RequestDepartmentService departments, CancellationToken ct) =>
                departments.UpdateAsync(id, departmentId, body, ct));
        requests.MapGet("/{id:guid}/department-query", (Guid id, RequestDepartmentService departments, CancellationToken ct) =>
            departments.QueryAsync(id, ct));
    }
}
