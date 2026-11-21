using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Documents;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Tests.Infrastructure;
using static ClerkAssistant.Api.Tests.Register.RegisterApi;

namespace ClerkAssistant.Api.Tests.Register;

public class DocumentUploadTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public async Task Upload_with_fake_pdf_signature_is_rejected()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Koszt remontu fontanny", Today);

        var response = await client.PostAsync($"/api/requests/{id}/documents",
            UploadForm("odpowiedz.pdf", "<html>to nie jest PDF</html>"u8.ToArray()), Ct);

        Assert.Contains("file", (await response.ValidationErrorsAsync()).Keys);
        Assert.Empty((await client.GetRequestAsync(id)).Documents);
    }

    [Fact]
    public async Task Upload_without_csrf_token_is_rejected()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Koszt oświetlenia parku", Today);
        client.DefaultRequestHeaders.Remove(ApiClient.CsrfHeader);

        var response = await client.PostAsync($"/api/requests/{id}/documents", UploadForm("odpowiedz.txt", "Treść"u8.ToArray()), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await client.GetRequestAsync(id)).Documents);
    }

    [Fact]
    public async Task Department_reply_is_stored_downloadable_and_deletable()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var wgk = await client.DepartmentIdAsync("WGK");
        var id = await client.CreateRequestAsync("Liczba nasadzeń drzew w 2026 r.", new DateOnly(2026, 11, 10));
        const string text = "W 2026 r. posadzono 1 250 drzew.";

        var upload = await client.PostAsync($"/api/requests/{id}/documents",
            UploadForm("odpowiedź WGK.txt", Encoding.UTF8.GetBytes(text), nameof(DocumentKind.DepartmentReply), wgk), Ct);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var document = await upload.Content.ReadFromJsonAsync<DocumentDto>(ApiClient.Json, Ct);
        var detail = await client.GetRequestAsync(id);
        var download = await client.GetAsync($"/api/documents/{document!.Id}/content", Ct);
        var extracted = await client.GetFromJsonAsync<DocumentText>($"/api/documents/{document.Id}/text", ApiClient.Json, Ct);

        Assert.Equal(DocumentKind.DepartmentReply, document.Kind);
        Assert.Equal("WGK", document.DepartmentSymbol);
        Assert.True(document.HasText);
        Assert.Equal("Administrator", document.UploadedBy);
        Assert.Equal(document.Id, Assert.Single(detail.Documents).Id);
        Assert.Equal(Today, Assert.Single(detail.Departments).AnsweredOn);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("odpowiedź WGK.txt", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(text, await download.Content.ReadAsStringAsync(Ct));
        Assert.Equal(text, extracted!.Text);

        var delete = await client.DeleteAsync($"/api/documents/{document.Id}", Ct);
        var history = (await client.GetFromJsonAsync<HistoryItem[]>($"/api/requests/{id}/history", ApiClient.Json, Ct))!;

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty((await client.GetRequestAsync(id)).Documents);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/documents/{document.Id}/content", Ct)).StatusCode);
        Assert.Equal(
            ["document.deleted", "document.text-viewed", "document.downloaded", "document.uploaded"],
            history.Take(4).Select(h => h.Action));
        Assert.All(history.Take(4), h => Assert.Equal("Odpowiedź wydziału WGK (TXT)", h.Details));
        Assert.DoesNotContain(history, h => h.Details?.Contains(text) == true);
    }
}
