using WireMock.Server;
using WireMock.Settings;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;

namespace _10xGitHubPolicies.Tests.Integration.Fixtures;

public class GitHubApiFixture : IAsyncLifetime
{
    public WireMockServer MockServer { get; private set; } = null!;
    public string BaseUrl => MockServer.Url!;

    public async Task InitializeAsync()
    {
        // Configure .NET to accept self-signed certificates for WireMock
        // This must be done before creating any HttpClient instances
        ServicePointManager.ServerCertificateValidationCallback =
            (sender, certificate, chain, sslPolicyErrors) => true;

        MockServer = WireMockServer.Start(new WireMockServerSettings
        {
            UseSSL = true,
            Port = 0 // Random port
        });
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Reset certificate validation callback
        ServicePointManager.ServerCertificateValidationCallback = null;

        MockServer?.Stop();
        MockServer?.Dispose();
        await Task.CompletedTask;
    }
}

