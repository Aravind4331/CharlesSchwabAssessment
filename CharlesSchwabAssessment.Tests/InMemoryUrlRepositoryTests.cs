using CharlesSchwabAssessment.Models;
using CharlesSchwabAssessment.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CharlesSchwabAssessment.Tests;

[TestClass]
public class InMemoryUrlRepositoryTests
{
    [TestMethod]
    public async Task TryAddAsync_WithNewCode_ReturnsTrue()
    {
        var repository = new InMemoryUrlRepository();
        var record = CreateRecord("abc123");

        var added = await repository.TryAddAsync(record, CancellationToken.None);

        Assert.IsTrue(added);
    }

    [TestMethod]
    public async Task TryAddAsync_WithDuplicateCode_ReturnsFalse()
    {
        var repository = new InMemoryUrlRepository();
        var record = CreateRecord("dup001");

        await repository.TryAddAsync(record, CancellationToken.None);
        var addedAgain = await repository.TryAddAsync(CreateRecord("dup001"), CancellationToken.None);

        Assert.IsFalse(addedAgain);
    }

    [TestMethod]
    public async Task GetAsync_IsCaseInsensitive()
    {
        var repository = new InMemoryUrlRepository();
        await repository.TryAddAsync(CreateRecord("MixedCase"), CancellationToken.None);

        var record = await repository.GetAsync("mixedcase", CancellationToken.None);

        Assert.IsNotNull(record);
        Assert.AreEqual("MixedCase", record.Code);
    }

    [TestMethod]
    public async Task UpdateAsync_WithUnknownCode_ReturnsFalse()
    {
        var repository = new InMemoryUrlRepository();

        var updated = await repository.UpdateAsync(CreateRecord("unknown"), CancellationToken.None);

        Assert.IsFalse(updated);
    }

    private static UrlRecord CreateRecord(string code)
    {
        return new UrlRecord
        {
            Code = code,
            OriginalUrl = new Uri("https://example.com"),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
