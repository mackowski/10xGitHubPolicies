using System.Security.Cryptography;
using System.Text;
using _10xGitHubPolicies.App.Controllers;
using _10xGitHubPolicies.App.Services.Webhooks;
using Bogus;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace _10xGitHubPolicies.Tests.Controllers;

[Trait("Category", "Unit")]
[Trait("Service", "WebhookController")]
public class WebhookControllerTests
{
    private readonly ILogger<WebhookController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebhookService _webhookService;
    private readonly WebhookController _sut;
    private readonly Faker _faker;
    private readonly string _webhookSecret;

    public WebhookControllerTests()
    {
        _logger = Substitute.For<ILogger<WebhookController>>();
        _webhookService = Substitute.For<IWebhookService>();
        _faker = new Faker();
        _webhookSecret = _faker.Random.AlphaNumeric(32);

        // Setup configuration mock
        var configurationDict = new Dictionary<string, string?>
        {
            { "GitHubApp:WebhookSecret", _webhookSecret }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationDict)
            .Build();

        _sut = new WebhookController(_logger, _configuration, _webhookService);
    }

    [Fact]
    public async Task HandleWebhook_WhenValidSignature_Returns200()
    {
        // Arrange
        var payload = """{"action": "opened", "pull_request": {"number": 1}}""";
        var signature = ComputeSignature(payload, _webhookSecret);
        var context = CreateHttpContext(payload, signature, "pull_request", "opened", "delivery-123");

        _sut.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = context
        };

        // Act
        var result = await _sut.HandleWebhook();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
        await _webhookService.Received(1).ProcessPullRequestEventAsync(
            "pull_request",
            "opened",
            Arg.Any<string>(),
            "delivery-123");
    }

    [Fact]
    public async Task HandleWebhook_WhenInvalidSignature_Returns401()
    {
        // Arrange
        var payload = """{"action": "opened", "pull_request": {"number": 1}}""";
        var invalidSignature = "sha256=invalid_signature";
        var context = CreateHttpContext(payload, invalidSignature, "pull_request", "opened", "delivery-123");

        _sut.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = context
        };

        // Act
        var result = await _sut.HandleWebhook();

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        await _webhookService.DidNotReceive().ProcessPullRequestEventAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task HandleWebhook_WhenMissingSignature_Returns401()
    {
        // Arrange
        var payload = """{"action": "opened", "pull_request": {"number": 1}}""";
        var context = CreateHttpContext(payload, null, "pull_request", "opened", "delivery-123", includeSignature: false);

        _sut.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = context
        };

        // Act
        var result = await _sut.HandleWebhook();

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        await _webhookService.DidNotReceive().ProcessPullRequestEventAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task HandleWebhook_WhenWebhookSecretNotConfigured_Returns401()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();
        var controller = new WebhookController(_logger, emptyConfig, _webhookService);

        var payload = """{"action": "opened", "pull_request": {"number": 1}}""";
        var signature = ComputeSignature(payload, _webhookSecret);
        var context = CreateHttpContext(payload, signature, "pull_request", "opened", "delivery-123");

        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = context
        };

        // Act
        var result = await controller.HandleWebhook();

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        await _webhookService.DidNotReceive().ProcessPullRequestEventAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task HandleWebhook_WhenPullRequestOpened_EnqueuesProcessingJob()
    {
        // Arrange
        var payload = """{"action": "opened", "pull_request": {"number": 1}}""";
        var signature = ComputeSignature(payload, _webhookSecret);
        var context = CreateHttpContext(payload, signature, "pull_request", "opened", "delivery-123");

        _sut.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = context
        };

        // Act
        var result = await _sut.HandleWebhook();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        await _webhookService.Received(1).ProcessPullRequestEventAsync(
            "pull_request",
            "opened",
            payload,
            "delivery-123");
    }

    [Fact]
    public async Task HandleWebhook_WhenPullRequestSynchronized_EnqueuesProcessingJob()
    {
        // Arrange
        var payload = """{"action": "synchronize", "pull_request": {"number": 1}}""";
        var signature = ComputeSignature(payload, _webhookSecret);
        var context = CreateHttpContext(payload, signature, "pull_request", "synchronize", "delivery-456");

        _sut.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = context
        };

        // Act
        var result = await _sut.HandleWebhook();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        await _webhookService.Received(1).ProcessPullRequestEventAsync(
            "pull_request",
            "synchronize",
            payload,
            "delivery-456");
    }

    [Fact]
    public async Task HandleWebhook_WhenPingEvent_ReturnsPong()
    {
        // Arrange
        var payload = """{"zen": "Keep it logically awesome."}""";
        var signature = ComputeSignature(payload, _webhookSecret);
        var context = CreateHttpContext(payload, signature, "ping", null, "delivery-ping");

        _sut.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = context
        };

        // Act
        var result = await _sut.HandleWebhook();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var value = okResult!.Value;
        value.Should().NotBeNull();
        value!.GetType().GetProperty("message")!.GetValue(value).Should().Be("pong");
        await _webhookService.DidNotReceive().ProcessPullRequestEventAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task HandleWebhook_WhenUnsupportedEvent_Returns200()
    {
        // Arrange
        var payload = """{"action": "created"}""";
        var signature = ComputeSignature(payload, _webhookSecret);
        var context = CreateHttpContext(payload, signature, "repository", "created", "delivery-789");

        _sut.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = context
        };

        // Act
        var result = await _sut.HandleWebhook();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        await _webhookService.DidNotReceive().ProcessPullRequestEventAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>());
    }

    // Helper Methods

    private static string ComputeSignature(string payload, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var hashHex = Convert.ToHexString(hash).ToLowerInvariant();

        return $"sha256={hashHex}";
    }

    private HttpContext CreateHttpContext(
        string payload,
        string? signature,
        string eventType,
        string? action,
        string? deliveryId,
        bool includeSignature = true)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;

        // Set headers
        if (includeSignature && !string.IsNullOrEmpty(signature))
        {
            request.Headers["X-Hub-Signature-256"] = signature;
        }

        request.Headers["X-GitHub-Event"] = eventType;
        if (!string.IsNullOrEmpty(action))
        {
            request.Headers["X-GitHub-Event-Action"] = action;
        }

        if (!string.IsNullOrEmpty(deliveryId))
        {
            request.Headers["X-GitHub-Delivery"] = deliveryId;
        }

        // Set body
        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        request.Body = new MemoryStream(bodyBytes);
        request.ContentType = "application/json";
        request.ContentLength = bodyBytes.Length;

        return context;
    }
}

