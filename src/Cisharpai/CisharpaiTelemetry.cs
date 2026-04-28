using System.Diagnostics;

namespace Cisharpai;

/// <summary>
/// Names and primitives used by Cisharpai for distributed tracing.
///
/// The library emits one client-kind <see cref="Activity"/> per HTTP call from
/// <see cref="LlmHttpClient"/>. Activities are only created when a listener is
/// registered for <see cref="ActivitySourceName"/>; otherwise the cost is zero.
///
/// Consumers can subscribe in any of three ways:
/// <list type="bullet">
///   <item>Do nothing — log entries still pick up TraceId/SpanId from any
///   ambient <c>Activity.Current</c> when the logger has
///   <c>ActivityTrackingOptions.TraceId | SpanId</c>.</item>
///   <item>Register a plain <c>System.Diagnostics.ActivityListener</c>.</item>
///   <item>Add the source to OpenTelemetry, e.g.
///   <c>AddOpenTelemetry().WithTracing(b =&gt; b.AddSource(CisharpaiTelemetry.ActivitySourceName))</c>.</item>
/// </list>
/// </summary>
public static class CisharpaiTelemetry
{
    /// <summary>
    /// The <see cref="ActivitySource"/> name used by the library.
    /// Pass this to OpenTelemetry's <c>AddSource(...)</c> or to an
    /// <c>ActivityListener.ShouldListenTo</c> callback.
    /// </summary>
    public const string ActivitySourceName = "Cisharpai";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
