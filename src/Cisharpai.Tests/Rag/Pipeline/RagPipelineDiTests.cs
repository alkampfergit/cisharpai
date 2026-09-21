using Cisharpai.Rag.Pipeline;
using Cisharpai.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Rag.Pipeline;

[TestFixture]
public class RagPipelineDiTests
{
    [Test]
    public void AddCisharpaiRagPipeline_RegistersSingleton()
    {
        var services = new ServiceCollection();
        var retriever = FakeResponses.Retriever();

        services.AddCisharpaiRagPipeline(builder =>
        {
            builder.WithRetriever(retriever);
        });

        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IRagPipeline>();

        Assert.That(pipeline, Is.Not.Null);

        var pipeline2 = provider.GetRequiredService<IRagPipeline>();
        Assert.That(pipeline2, Is.SameAs(pipeline));
    }

    [Test]
    public void AddCisharpaiRagPipeline_ServiceProviderOverload_RegistersScoped()
    {
        var services = new ServiceCollection();
        var retriever = FakeResponses.Retriever();

        services.AddCisharpaiRagPipeline((sp, builder) =>
        {
            builder.WithRetriever(retriever);
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IRagPipeline>();

        Assert.That(pipeline, Is.Not.Null);
    }

    [Test]
    public void AddCisharpaiRagPipeline_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddCisharpaiRagPipeline((Action<RagPipelineBuilder>)null!));
    }
}
