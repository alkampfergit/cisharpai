namespace Cisharpai.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        // Example test using Assert.That syntax
        var result = 1 + 1;
        Assert.That(result, Is.EqualTo(2));
    }
}
