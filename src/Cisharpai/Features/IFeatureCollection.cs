namespace Cisharpai.Features;

/// <summary>
/// A collection of features that can be discovered and accessed by type.
/// Provides a type-safe way to query optional capabilities of a client.
/// </summary>
public interface IFeatureCollection : IEnumerable<KeyValuePair<Type, object>>
{
    /// <summary>
    /// Gets the feature of the specified type, or null if not available.
    /// </summary>
    T? Get<T>() where T : class;

    /// <summary>
    /// Registers a feature instance for the specified type.
    /// </summary>
    void Set<T>(T instance) where T : class;
}
