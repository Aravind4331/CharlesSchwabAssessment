using CharlesSchwabAssessment.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CharlesSchwabAssessment.Tests;

[TestClass]
public class ShortCodeGeneratorTests
{
    [TestMethod]
    public void Generate_DefaultLength_ReturnsEightCharacters()
    {
        var generator = new ShortCodeGenerator();

        var code = generator.Generate();

        Assert.AreEqual(8, code.Length);
    }

    [TestMethod]
    public void Generate_WithCustomLength_ReturnsRequestedLength()
    {
        var generator = new ShortCodeGenerator();

        var code = generator.Generate(12);

        Assert.AreEqual(12, code.Length);
    }

    [TestMethod]
    public void Generate_WithLengthBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        var generator = new ShortCodeGenerator();

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => generator.Generate(2));
    }

    [TestMethod]
    public void Generate_ProducesOnlyAlphanumericCharacters()
    {
        var generator = new ShortCodeGenerator();

        var code = generator.Generate(32);

        Assert.IsTrue(code.All(char.IsLetterOrDigit));
    }
}
