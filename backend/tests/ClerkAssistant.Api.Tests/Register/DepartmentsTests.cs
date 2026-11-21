using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Departments;
using ClerkAssistant.Api.Tests.Infrastructure;
using static ClerkAssistant.Api.Tests.Register.RegisterApi;

namespace ClerkAssistant.Api.Tests.Register;

public class DepartmentsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Departments_are_listed_before_offices_and_inactive_ones_are_hidden_by_default()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var all = (await client.GetFromJsonAsync<DepartmentDto[]>("/api/departments", ApiClient.Json, Ct))!;
        var bkz = all.Single(d => d.Symbol == "BKZ");

        var update = await client.PutAsJsonAsync($"/api/admin/departments/{bkz.Id}",
            new UpdateDepartmentRequest(" Biuro Miejskiego Konserwatora Zabytków ", bkz.Scope, null, false), ApiClient.Json, Ct);
        var active = await client.GetFromJsonAsync<DepartmentDto[]>("/api/departments", ApiClient.Json, Ct);
        var withInactive = await client.GetFromJsonAsync<DepartmentDto[]>("/api/departments?includeInactive=true", ApiClient.Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(all.OrderBy(d => d.Kind).Select(d => d.Kind), all.Select(d => d.Kind));
        Assert.Equal(DepartmentKind.Department, all[0].Kind);
        Assert.DoesNotContain(active!, d => d.Symbol == "BKZ");
        var updated = withInactive!.Single(d => d.Symbol == "BKZ");
        Assert.Equal("Biuro Miejskiego Konserwatora Zabytków", updated.Name);
        Assert.Null(updated.Email);
        Assert.False(updated.IsActive);
    }
}
