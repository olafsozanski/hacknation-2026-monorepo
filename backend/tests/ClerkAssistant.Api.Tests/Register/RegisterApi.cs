using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Departments;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace ClerkAssistant.Api.Tests.Register;

internal static class RegisterApi
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static void SetToday(this ApiFactory factory, DateOnly today) =>
        factory.Clock.Now = new DateTimeOffset(today.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(1));

    public static CreateRequestRequest NewRequest(
        string subject,
        DateOnly receivedOn,
        string? caseNumber = null,
        string? content = "Proszę o udostępnienie informacji.",
        Guid? sourceFileId = null,
        Guid[]? departmentIds = null) =>
        new(subject, content, "Jan Testowy", "jan.testowy@example.com", ReplyChannel.Email, receivedOn, null,
            caseNumber, null, sourceFileId, departmentIds);

    public static async Task<Guid> CreateRequestAsync(this HttpClient client, CreateRequestRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/requests", request, ApiClient.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(ApiClient.Json, Ct);
        return created!.Id;
    }

    public static Task<Guid> CreateRequestAsync(this HttpClient client, string subject, DateOnly receivedOn) =>
        client.CreateRequestAsync(NewRequest(subject, receivedOn));

    public static async Task<RequestDetail> GetRequestAsync(this HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<RequestDetail>($"/api/requests/{id}", ApiClient.Json, Ct))!;

    public static async Task<RequestDetail> ChangeStatusAsync(
        this HttpClient client, Guid id, RequestStatus status, DateOnly? respondedOn = null)
    {
        var current = await client.GetRequestAsync(id);
        var response = await client.PostAsJsonAsync($"/api/requests/{id}/status",
            new ChangeStatusRequest(status, respondedOn, current.Version), ApiClient.Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RequestDetail>(ApiClient.Json, Ct))!;
    }

    public static async Task<Guid> DepartmentIdAsync(this HttpClient client, string symbol)
    {
        var departments = await client.GetFromJsonAsync<DepartmentDto[]>("/api/departments", ApiClient.Json, Ct);
        return departments!.Single(d => d.Symbol == symbol).Id;
    }

    public static async Task<IDictionary<string, string[]>> ValidationErrorsAsync(this HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(ApiClient.Json, Ct);
        return problem!.Errors;
    }
}
