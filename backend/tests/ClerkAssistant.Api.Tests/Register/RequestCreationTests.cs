using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static ClerkAssistant.Api.Tests.Register.RegisterApi;

namespace ClerkAssistant.Api.Tests.Register;

public class RequestCreationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public async Task Create_computes_due_date_and_next_case_number()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();

        var first = await client.GetRequestAsync(await client.CreateRequestAsync("Koszt iluminacji świątecznej", new DateOnly(2026, 11, 7)));
        var second = await client.GetRequestAsync(await client.CreateRequestAsync("Liczba ławek w parkach", new DateOnly(2026, 11, 18)));

        Assert.Equal(new DateOnly(2026, 11, 23), first.DueOn);
        Assert.Equal(new DateOnly(2027, 1, 7), first.LatestExtendedDueOn);
        Assert.Equal(2026, first.Year);
        Assert.Equal($"BKS.IV.1431.{first.SequenceNumber}.2026", first.CaseNumber);
        Assert.Equal(first.SequenceNumber + 1, second.SequenceNumber);
        Assert.Equal($"BKS.IV.1431.{second.SequenceNumber}.2026", second.CaseNumber);
        Assert.Equal(new DateOnly(2026, 12, 2), second.DueOn);
        Assert.Equal(RequestStatus.NoAction, first.Status);
        Assert.Equal(Urgency.OnTrack, first.Urgency);
        Assert.Equal(5, first.DaysLeft);
        Assert.Equal("Administrator", first.Assignee?.FullName);
    }

    [Fact]
    public async Task Create_rejects_future_received_date()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/requests",
            NewRequest("Wniosek z przyszłości", Today.AddDays(1)), ApiClient.Json, Ct);

        Assert.Contains("receivedOn", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Contract_annotations_are_validated()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Wniosek do walidacji", Today);
        var version = (await client.GetRequestAsync(id)).Version;

        var missingSubject = await client.PostAsJsonAsync("/api/requests", NewRequest("", Today), ApiClient.Json, Ct);
        var zeroDays = await client.PostAsJsonAsync($"/api/requests/{id}/extension",
            new ExtendDeadlineRequest(0, "Powód", version), ApiClient.Json, Ct);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, missingSubject.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, zeroDays.StatusCode);
        Assert.Equal(RequestStatus.NoAction, (await client.GetRequestAsync(id)).Status);
    }

    [Fact]
    public async Task Create_rejects_duplicate_case_number_but_still_assigns_a_sequence()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();

        var id = await client.CreateRequestAsync(NewRequest("Wniosek z ręcznym znakiem", Today, caseNumber: "BKS.IV.1431.900.2026"));
        var duplicate = await client.PostAsJsonAsync("/api/requests",
            NewRequest("Drugi wniosek z tym samym znakiem", Today, caseNumber: " BKS.IV.1431.900.2026 "), ApiClient.Json, Ct);

        var detail = await client.GetRequestAsync(id);
        Assert.Equal("BKS.IV.1431.900.2026", detail.CaseNumber);
        Assert.True(detail.SequenceNumber > 0);
        Assert.Contains("caseNumber", (await duplicate.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Create_rejects_unknown_department()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/requests",
            NewRequest("Wniosek do nieznanego wydziału", Today, departmentIds: [Guid.NewGuid()]), ApiClient.Json, Ct);

        Assert.Contains("departmentIds", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Create_from_uploaded_file_attaches_it_and_uses_its_text()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var fileId = await StoreFileAsync("Wnoszę o udostępnienie liczby wydanych kart parkingowych.");
        var wgk = await client.DepartmentIdAsync("WGK");

        var detail = await client.GetRequestAsync(await client.CreateRequestAsync(
            NewRequest("Karty parkingowe", Today, content: " ", sourceFileId: fileId, departmentIds: [wgk, wgk])));
        var reuse = await client.PostAsJsonAsync("/api/requests",
            NewRequest("Ponowne użycie pliku", Today, sourceFileId: fileId), ApiClient.Json, Ct);

        Assert.Equal("Wnoszę o udostępnienie liczby wydanych kart parkingowych.", detail.Content);
        var document = Assert.Single(detail.Documents);
        Assert.Equal(DocumentKind.Request, document.Kind);
        Assert.Equal("wniosek.txt", document.FileName);
        Assert.True(document.HasText);
        Assert.Equal("Administrator", document.UploadedBy);
        Assert.Equal("WGK", Assert.Single(detail.Departments).Symbol);
        Assert.Contains("sourceFileId", (await reuse.ValidationErrorsAsync()).Keys);
    }

    private async Task<Guid> StoreFileAsync(string text)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var file = new StoredFile
        {
            FileName = "wniosek.txt",
            ContentType = "text/plain; charset=utf-8",
            Content = System.Text.Encoding.UTF8.GetBytes(text),
            SizeBytes = text.Length,
            Sha256 = new string('0', 64),
            ExtractedText = text,
            CreatedAt = factory.Clock.GetUtcNow(),
        };
        db.Files.Add(file);
        await db.SaveChangesAsync(Ct);
        return file.Id;
    }
}
