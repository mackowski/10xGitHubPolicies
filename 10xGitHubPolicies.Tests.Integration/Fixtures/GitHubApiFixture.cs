using WireMock.Server;
using WireMock.Settings;

namespace _10xGitHubPolicies.Tests.Integration.Fixtures;

public class GitHubApiFixture : IAsyncLifetime
{
    public WireMockServer MockServer { get; private set; } = null!;
    public string BaseUrl => MockServer.Url!;
    
    public async Task InitializeAsync()
    {
        MockServer = WireMockServer.Start(new WireMockServerSettings
        {
            UseSSL = true,
            Port = 0 // Random port
        });
        await Task.CompletedTask;
    }
    
    public async Task DisposeAsync()
    {
        MockServer?.Stop();
        MockServer?.Dispose();
        await Task.CompletedTask;
    }
}

