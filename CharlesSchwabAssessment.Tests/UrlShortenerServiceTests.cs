using CharlesSchwabAssessment.Models;
using CharlesSchwabAssessment.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CharlesSchwabAssessment.Tests;

[TestClass]
public class UrlShortenerServiceTests
{
    [TestMethod]
    public async Task ShortenAsync_WithCustomAliasAndTtl_ReturnsExpectedResponse()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = new UrlShortenerService(new InMemoryUrlRepository(), new ShortCodeGenerator(), timeProvider);

        var response = await service.ShortenAsync(
            new ShortenUrlRequest
            {
                Url = "https://example.com/docs",
                CustomAlias = " docs-guide ",
                TtlMinutes = 30
            },
            new Uri("https://sho.rt/"),
            CancellationToken.None);

        Assert.AreEqual("docs-guide", response.Code);
        Assert.AreEqual("https://sho.rt/u/docs-guide", response.ShortUrl);
        Assert.AreEqual("https://example.com/docs", response.OriginalUrl);
        Assert.AreEqual(timeProvider.GetUtcNow().AddMinutes(30), response.ExpiresAtUtc);
    }

    [TestMethod]
    public async Task RegisterRedirectAsync_UpdatesAnalytics()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = new UrlShortenerService(new InMemoryUrlRepository(), new ShortCodeGenerator(), timeProvider);

        await service.ShortenAsync(
            new ShortenUrlRequest
            {
                Url = "https://example.com/analytics",
                CustomAlias = "analytics"
            },
            new Uri("https://sho.rt/"),
            CancellationToken.None);

        var accessedAt = timeProvider.GetUtcNow().AddMinutes(5);
        var updated = await service.RegisterRedirectAsync("analytics", accessedAt, CancellationToken.None);
        var analytics = await service.GetAnalyticsAsync("analytics", CancellationToken.None);

        Assert.IsTrue(updated);
        Assert.IsNotNull(analytics);
        Assert.AreEqual(1, analytics.RedirectCount);
        Assert.AreEqual(accessedAt, analytics.LastAccessedAtUtc);
    }

    [TestMethod]
    public async Task ShortenAsync_WithInvalidUrl_ThrowsUrlShorteningException()
    {
        var service = new UrlShortenerService(new InMemoryUrlRepository(), new ShortCodeGenerator(), TimeProvider.System);

        await Assert.ThrowsExceptionAsync<UrlShorteningException>(() => service.ShortenAsync(
            new ShortenUrlRequest
            {
                Url = "notaurl",
                CustomAlias = "bad"
            },
            new Uri("https://sho.rt/"),
            CancellationToken.None));
    }
}
