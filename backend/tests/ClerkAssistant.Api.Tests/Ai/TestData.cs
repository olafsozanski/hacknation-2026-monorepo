using System.Security.Cryptography;
using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClerkAssistant.Api.Tests.Ai;

public sealed record Assignment(string Symbol, DateOnly? AskedOn = null, DateOnly? AnsweredOn = null);

public sealed record NewRequest(string Subject, string Content, DateOnly ReceivedOn)
{
    public RequestStatus Status { get; init; } = RequestStatus.NoAction;
    public string? RequesterName { get; init; }
    public string? RequesterContact { get; init; }
    public Assignment[] Departments { get; init; } = [];
}

public static class TestData
{
    private static int _sequence = 1000;

    public static async Task<Guid> AddRequestAsync(this ApiFactory factory, NewRequest data)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sequence = Interlocked.Increment(ref _sequence);
        var request = new InfoRequest
        {
            Year = data.ReceivedOn.Year,
            SequenceNumber = sequence,
            CaseNumber = $"BKS.IV.1431.{sequence}.{data.ReceivedOn.Year}",
            Subject = data.Subject,
            Content = data.Content,
            RequesterName = data.RequesterName,
            RequesterContact = data.RequesterContact,
            ReceivedOn = data.ReceivedOn,
            DueOn = Deadlines.InitialDueDate(data.ReceivedOn),
            Status = data.Status,
            CreatedAt = factory.Clock.GetUtcNow(),
            UpdatedAt = factory.Clock.GetUtcNow(),
        };
        foreach (var assignment in data.Departments)
        {
            var department = await db.Departments.SingleAsync(d => d.Symbol == assignment.Symbol);
            request.Departments.Add(new RequestDepartment
            {
                DepartmentId = department.Id,
                AskedOn = assignment.AskedOn,
                AnsweredOn = assignment.AnsweredOn,
            });
        }

        db.Requests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    public static async Task AddDepartmentReplyAsync(this ApiFactory factory, Guid requestId, string symbol, string text)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var department = await db.Departments.SingleAsync(d => d.Symbol == symbol);
        var admin = await db.Users.SingleAsync(u => u.UserName == ApiFactory.AdminUserName);
        var content = Encoding.UTF8.GetBytes(text);
        db.RequestDocuments.Add(new RequestDocument
        {
            RequestId = requestId,
            Kind = DocumentKind.DepartmentReply,
            DepartmentId = department.Id,
            UploadedAt = factory.Clock.GetUtcNow(),
            UploadedById = admin.Id,
            File = new StoredFile
            {
                FileName = $"odpowiedz-{symbol}.txt",
                ContentType = "text/plain; charset=utf-8",
                SizeBytes = content.Length,
                Content = content,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
                ExtractedText = text,
                CreatedAt = factory.Clock.GetUtcNow(),
                CreatedById = admin.Id,
            },
        });
        await db.SaveChangesAsync();
    }

    public static async Task<T> QueryAsync<T>(this ApiFactory factory, Func<AppDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public static MultipartFormDataContent TextFile(string fileName, string text, params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", fileName);
        foreach (var (name, value) in fields)
        {
            form.Add(new StringContent(value), name);
        }

        return form;
    }
}
