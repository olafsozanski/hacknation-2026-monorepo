using ClerkAssistant.Api.Infrastructure.Ai;

namespace ClerkAssistant.Api.Tests.Security;

public class AiOptionsTests
{
    [Theory]
    [InlineData("http://ollama:11434/v1", true)]
    [InlineData("http://localhost:11434/v1", true)]
    [InlineData("http://192.168.10.5:11434/v1", true)]
    [InlineData("http://ai.urzad.internal/v1", true)]
    [InlineData("https://api.openai.com/v1", false)]
    [InlineData("http://8.8.8.8/v1", false)]
    [InlineData(null, false)]
    public void Local_mode_requires_an_internal_endpoint(string? endpoint, bool expected)
    {
        var options = new AiOptions { IsLocal = true, Endpoint = endpoint };

        Assert.Equal(expected, options.IsConfigured);
    }

    [Fact]
    public void External_provider_needs_explicit_opt_in()
    {
        Assert.False(new AiOptions { ApiKey = "sk-test" }.IsConfigured);
        Assert.True(new AiOptions { ApiKey = "sk-test", AllowExternalProcessing = true }.IsConfigured);
    }
}
