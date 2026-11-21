using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ClerkAssistant.Api.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Test-Admin-2026-Pass";

    private readonly string _database = $"udip_test_{Guid.NewGuid():N}";

    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(1)));

    private static string ServerConnectionString =>
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? "Host=localhost;Port=5432;Username=udip;Password=udip_dev_password";

    protected virtual IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", $"{ServerConnectionString};Database={_database}");
        builder.UseSetting("Seed:AdminUserName", AdminUserName);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:DemoData", "false");
        builder.UseSetting("Ai:ApiKey", "");
        builder.UseSetting("Ai:IsLocal", "false");
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection($"{ServerConnectionString};Database=postgres");
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        GC.SuppressFinalize(this);
    }
}
