using _10xGitHubPolicies.App.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using Octokit.Internal;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace _10xGitHubPolicies.App.Services;

public class GitHubService : IGitHubService
{
    private readonly GitHubAppOptions _options;
    private readonly ILogger<GitHubService> _logger;
    private readonly IMemoryCache _cache;
    private const string InstallationTokenCacheKey = "GitHubInstallationToken";

    public GitHubService(IOptions<GitHubAppOptions> options, ILogger<GitHubService> logger, IMemoryCache cache)
    {
        _options = options.Value;
        _logger = logger;
        _cache = cache;
    }

    public async Task<GitHubClient> GetAuthenticatedClient()
    {
        var token = await _cache.GetOrCreateAsync(InstallationTokenCacheKey, async entry =>
        {
            _logger.LogInformation("Installation token not found in cache. Generating a new one.");
            
            var jwt = GetJwt();
            var appClient = new GitHubClient(new ProductHeaderValue("10xGitHubPolicies"), new InMemoryCredentialStore(new Credentials(jwt, AuthenticationType.Bearer)));
            
            var tokenResponse = await appClient.GitHubApps.CreateInstallationToken(_options.InstallationId);

            entry.AbsoluteExpiration = tokenResponse.ExpiresAt.AddMinutes(-5); // Cache for 55 minutes
            
            _logger.LogInformation("Successfully generated a new installation token, expiring at {Expiry}", tokenResponse.ExpiresAt);
            
            return tokenResponse.Token;
        });

        return new GitHubClient(new ProductHeaderValue("10xGitHubPolicies"), new InMemoryCredentialStore(new Credentials(token)));
    }
    
    private string GetJwt()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(_options.PrivateKey);
        var key = new RsaSecurityKey(rsa);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.AppId.ToString(),
            IssuedAt = DateTime.UtcNow.AddMinutes(-1), // Add a 1-minute buffer
            Expires = DateTime.UtcNow.AddMinutes(9), // Expires in 9 minutes
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
