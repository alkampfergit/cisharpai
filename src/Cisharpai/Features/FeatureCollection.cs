using System.Collections;
using System.Collections.Concurrent;

namespace Cisharpai.Features;

/// <summary>
/// Default implementation of <see cref="IFeatureCollection"/> backed by a thread-safe dictionary.
/// </summary>
public sealed class FeatureCollection : IFeatureCollection
{
    private readonly ConcurrentDictionary<Type, object> _features = new();

    /// <inheritdoc />
    public T? Get<T>() where T : class
    {
        return _features.TryGetValue(typeof(T), out var value) ? (T)value : null;
    }

    /// <inheritdoc />
    public void Set<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _features[typeof(T)] = instance;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
