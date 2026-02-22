using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Core;

public sealed class HttpClientBuilderExtensionsTests
{
    [Test]
    public void AddCisharpaiResilienceHandler_Returns_Same_Builder_Instance()
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("test");
        var returnedBuilder = builder.AddCisharpaiResilienceHandler();

        // The extension method should return the same builder (fluent API)
        Assert.That(returnedBuilder, Is.SameAs(builder));
    }

    [Test]
    public void AddCisharpaiStreamingResilienceHandler_Returns_Same_Builder_Instance()
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("test2");
        var returnedBuilder = builder.AddCisharpaiStreamingResilienceHandler();

        Assert.That(returnedBuilder, Is.SameAs(builder));
    }

    [Test]
    public void AddCisharpaiStreamingResilienceHandler_Can_Build_ServiceProvider()
    {
        // Verify that the DI registration completes without exceptions
        var services = new ServiceCollection();
        services.AddHttpClient("streaming-client")
            .AddCisharpaiStreamingResilienceHandler();

        // Should not throw when building the provider
        Assert.DoesNotThrow(() =>
        {
            using var provider = services.BuildServiceProvider();
        });
    }

    [Test]
    public void Both_Handlers_Can_Be_Registered_Together()
    {
        // Both methods can be used independently on different named clients
        var services = new ServiceCollection();
        services.AddHttpClient("standard").AddCisharpaiResilienceHandler();
        services.AddHttpClient("streaming").AddCisharpaiStreamingResilienceHandler();

        Assert.DoesNotThrow(() =>
        {
            using var provider = services.BuildServiceProvider();
        });
    }

    [Test]
    public void AddCisharpaiResilienceHandler_Can_Build_ServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("standard-client")
            .AddCisharpaiResilienceHandler();

        Assert.DoesNotThrow(() =>
        {
            using var provider = services.BuildServiceProvider();
        });
    }
}
