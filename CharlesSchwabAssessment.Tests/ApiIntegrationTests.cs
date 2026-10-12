using System.Net;
using System.Net.Http.Json;
using CharlesSchwabAssessment.Models;
using CharlesSchwabAssessment.Orchestration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CharlesSchwabAssessment.Tests;

[TestClass]
public class ApiIntegrationTests
{
    private const string TestApiKey = "test-orchestration-key";

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Security:OrchestrationApiKey"] = TestApiKey
                    });
                });
            });
    }

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<Program> factory, bool allowAutoRedirect = true)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);
        return client;
    }

    [TestMethod]
    public async Task PostUrls_WithValidUrl_ReturnsCreatedShortUrl()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/urls", new ShortenUrlRequest
        {
            Url = "https://example.com/integration-test"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShortenUrlResponse>();
        Assert.IsNotNull(body);
        Assert.IsFalse(string.IsNullOrWhiteSpace(body.Code));
    }

    [TestMethod]
    public async Task PostUrls_WithInvalidUrl_ReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/urls", new ShortenUrlRequest
        {
            Url = "not-a-valid-url"
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetRedirect_WithUnknownCode_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/u/does-not-exist");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task ShortenThenRedirect_RoundTrips_AndTracksAnalytics()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var createResponse = await client.PostAsJsonAsync("/api/urls", new ShortenUrlRequest
        {
            Url = "https://example.com/round-trip",
            CustomAlias = "round-trip"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ShortenUrlResponse>();
        Assert.IsNotNull(created);

        var redirectResponse = await client.GetAsync($"/u/{created.Code}");
        Assert.AreEqual(HttpStatusCode.Redirect, redirectResponse.StatusCode);

        var analyticsResponse = await client.GetAsync($"/api/urls/{created.Code}/analytics");
        Assert.AreEqual(HttpStatusCode.OK, analyticsResponse.StatusCode);
        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<UrlAnalyticsResponse>();
        Assert.IsNotNull(analytics);
        Assert.AreEqual(1, analytics.RedirectCount);
    }

    [TestMethod]
    public async Task OrchestrationRuns_StartAndFetchSnapshot_Succeeds()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var startResponse = await client.PostAsJsonAsync("/api/orchestration/runs", new WorkflowRunRequest
        {
            WorkflowName = "Integration Test Workflow",
            Requirement = "Validate orchestration via HTTP",
            HighImpactChange = false,
            AmbiguousRequirement = false
        });

        Assert.AreEqual(HttpStatusCode.OK, startResponse.StatusCode);
        var run = await startResponse.Content.ReadFromJsonAsync<WorkflowRunResponse>();
        Assert.IsNotNull(run);

        var snapshotResponse = await client.GetAsync($"/api/orchestration/runs/{run.WorkflowId}");
        Assert.AreEqual(HttpStatusCode.OK, snapshotResponse.StatusCode);
    }

    [TestMethod]
    public async Task OrchestrationRuns_WithoutApiKey_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orchestration/runs", new WorkflowRunRequest
        {
            WorkflowName = "Unauthorized Attempt",
            Requirement = "Should be rejected",
            HighImpactChange = false,
            AmbiguousRequirement = false
        });

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task OrchestrationRuns_WithInvalidApiKey_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.PostAsJsonAsync("/api/orchestration/runs", new WorkflowRunRequest
        {
            WorkflowName = "Unauthorized Attempt",
            Requirement = "Should be rejected",
            HighImpactChange = false,
            AmbiguousRequirement = false
        });

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Approvals_WithoutApiKey_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/orchestration/approvals");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task UrlShortenerEndpoints_DoNotRequireApiKey()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/urls", new ShortenUrlRequest
        {
            Url = "https://example.com/no-auth-needed"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
