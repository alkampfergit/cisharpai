namespace Cisharpai.Features;

/// <summary>
/// Indicates that a client supports feature discovery via a feature collection.
/// </summary>
public interface IHasFeatures
{
    /// <summary>
    /// Gets the collection of optional features supported by this client.
    /// </summary>
    IFeatureCollection Features { get; }
}
