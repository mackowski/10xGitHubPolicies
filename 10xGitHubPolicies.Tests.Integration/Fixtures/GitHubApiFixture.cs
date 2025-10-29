using WireMock.Server;
using WireMock.Settings;
using System.Net.Http;

namespace _10xGitHubPolicies.Tests.Integration.Fixtures;

public class GitHubApiFixture : IAsyncLifetime
{
    public WireMockServer MockServer { get; private set; } = null!;
    public string BaseUrl => MockServer.Url!;
    public HttpClientHandler HttpClientHandler { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Create HttpClientHandler that accepts self-signed certificates
        // This is the .NET Core/.NET 5+ way to handle certificate validation for test scenarios
        // ServicePointManager is legacy and doesn't work reliably with HttpClient in .NET Core
        HttpClientHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };

        MockServer = WireMockServer.Start(new WireMockServerSettings
        {
            UseSSL = true,
            Port = 0 // Random port
        });
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        HttpClientHandler?.Dispose();
        MockServer?.Stop();
        MockServer?.Dispose();
        await Task.CompletedTask;
    }
}

