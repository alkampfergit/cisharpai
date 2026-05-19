namespace Cisharpai.Testing;

public static class FakeFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddFakeSupport(
        this ICisharpaiClientFactoryBuilder builder,
        FakeClientFactoryProvider? provider = null)
    {
        return builder.AddProvider(provider ?? new FakeClientFactoryProvider());
    }
}
