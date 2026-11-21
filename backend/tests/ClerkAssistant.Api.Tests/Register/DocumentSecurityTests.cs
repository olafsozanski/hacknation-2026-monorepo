using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Documents;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static ClerkAssistant.Api.Tests.Register.RegisterApi;

namespace ClerkAssistant.Api.Tests.Register;

public class DocumentPermissionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public async Task Only_the_uploader_or_an_admin_may_delete_a_document()
    {
        factory.SetToday(Today);
        var admin = await factory.CreateAuthenticatedClientAsync();
        var uploader = await factory.CreateClerkClientAsync(admin, "uploader");
        var colleague = await factory.CreateClerkClientAsync(admin, "colleague");
        var id = await admin.CreateRequestAsync("Wykaz umów najmu lokali użytkowych", Today);
        var first = await UploadAsync(uploader, id, "pierwsza.txt");
        var second = await UploadAsync(uploader, id, "druga.txt");

        var byColleague = await colleague.DeleteAsync($"/api/documents/{first}", Ct);
        var byUploader = await uploader.DeleteAsync($"/api/documents/{first}", Ct);
        var byAdmin = await admin.DeleteAsync($"/api/documents/{second}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, byColleague.StatusCode);
        var problem = await byColleague.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Json, Ct);
        Assert.Contains("tylko osoba, która go dodała", problem!.Detail);
        Assert.Equal(HttpStatusCode.NoContent, byUploader.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byAdmin.StatusCode);
        var history = await admin.GetFromJsonAsync<HistoryItem[]>($"/api/requests/{id}/history", ApiClient.Json, Ct);
        Assert.Equal(2, history!.Count(h => h.Action == "document.deleted"));
    }

    [Fact]
    public async Task Original_request_document_cannot_be_deleted_even_by_an_admin()
    {
        factory.SetToday(Today);
        var admin = await factory.CreateAuthenticatedClientAsync();
        var fileId = await factory.StoreFileAsync("Wnoszę o udostępnienie rejestru umów.");
        var id = await admin.CreateRequestAsync(NewRequest("Rejestr umów", Today, sourceFileId: fileId));
        var original = Assert.Single((await admin.GetRequestAsync(id)).Documents);

        var response = await admin.DeleteAsync($"/api/documents/{original.Id}", Ct);

        Assert.Equal(["Nie można usunąć oryginału wniosku."], (await response.ValidationErrorsAsync())["kind"]);
        Assert.Single((await admin.GetRequestAsync(id)).Documents);
    }

    private static async Task<Guid> UploadAsync(HttpClient client, Guid requestId, string fileName)
    {
        var response = await client.PostAsync($"/api/requests/{requestId}/documents",
            UploadForm(fileName, Encoding.UTF8.GetBytes("Informacja wydziału.")), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(ApiClient.Json, Ct))!.Id;
    }
}

public class DocumentUploadRateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Uploads_are_rate_limited_per_user()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Wniosek z wieloma załącznikami", new DateOnly(2026, 11, 18));

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 31; i++)
        {
            var response = await client.PostAsync($"/api/requests/{id}/documents",
                UploadForm("skan.pdf", "nie PDF"u8.ToArray()), Ct);
            statuses.Add(response.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(30));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }
}

public class EnumIntegrityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public async Task Undefined_enum_values_are_rejected()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Wniosek do sprawdzenia słowników", Today);
        var version = (await client.GetRequestAsync(id)).Version;

        var upload = await client.PostAsync($"/api/requests/{id}/documents",
            UploadForm("odpowiedz.txt", "Treść"u8.ToArray(), kind: "42"), Ct);
        var list = await client.GetAsync("/api/requests?status=42", Ct);
        var status = await client.PostAsJsonAsync($"/api/requests/{id}/status", new { status = 42, version }, ApiClient.Json, Ct);

        Assert.Contains("kind", (await upload.ValidationErrorsAsync()).Keys);
        Assert.Contains("status", (await list.ValidationErrorsAsync()).Keys);
        Assert.Equal(HttpStatusCode.BadRequest, status.StatusCode);
        var detail = await client.GetRequestAsync(id);
        Assert.Empty(detail.Documents);
        Assert.Equal(RequestStatus.NoAction, detail.Status);
    }
}
