using ClerkAssistant.Api.Tests.Infrastructure;

namespace ClerkAssistant.Api.Tests.Ai;

public class AiApiFactory : ApiFactory
{
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private HttpClient? _client;

    public async Task<HttpClient> ClientAsync()
    {
        await _clientLock.WaitAsync();
        try
        {
            return _client ??= await this.CreateAuthenticatedClientAsync();
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _clientLock.Dispose();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
