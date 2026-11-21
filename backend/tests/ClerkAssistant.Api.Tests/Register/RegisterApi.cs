using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Departments;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Features.Users;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

    public const string ClerkPassword = "Urzednik12345";

    public static async Task<HttpClient> CreateClerkClientAsync(this ApiFactory factory, HttpClient admin, string userName)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest(userName, $"Urzędnik {userName}", "UT", ClerkPassword, false), ApiClient.Json, Ct);
        created.EnsureSuccessStatusCode();
        return await factory.CreateAuthenticatedClientAsync(userName, ClerkPassword);
    }

    public static async Task<Guid> StoreFileAsync(this ApiFactory factory, string text, string fileName = "wniosek.txt")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var file = new StoredFile
        {
            FileName = fileName,
            ContentType = "text/plain; charset=utf-8",
            Content = Encoding.UTF8.GetBytes(text),
            SizeBytes = text.Length,
            Sha256 = new string('0', 64),
            ExtractedText = text,
            CreatedAt = factory.Clock.GetUtcNow(),
        };
        db.Files.Add(file);
        await db.SaveChangesAsync(Ct);
        return file.Id;
    }

    public static MultipartFormDataContent UploadForm(string fileName, byte[] content, string? kind = null, Guid? departmentId = null)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        if (kind is not null)
        {
            form.Add(new StringContent(kind), "kind");
        }

        if (departmentId is not null)
        {
            form.Add(new StringContent(departmentId.Value.ToString()), "departmentId");
        }

        return form;
    }

    public static async Task<IDictionary<string, string[]>> ValidationErrorsAsync(this HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(ApiClient.Json, Ct);
        return problem!.Errors;
    }
}
