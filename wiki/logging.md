# Logging and Tracing

Cisharpai emits structured logs through the standard `Microsoft.Extensions.Logging.ILogger<T>` API and optional distributed-tracing spans through `System.Diagnostics.ActivitySource`. The library never registers a sink or listener itself — everything you observe depends on how the consumer configures `ILoggerFactory` and (optionally) an activity listener or OpenTelemetry.

## What gets logged

Every HTTP call made by `LlmHttpClient` (used by every provider client) emits one or more entries with cached `LoggerMessage.Define` delegates and stable EventIds:

| EventId | Name | Level | Emitted on |
|---:|---|---|---|
| 1000 | `RequestStarted` | Information | Before sending a request |
| 1001 | `RequestCompleted` | Information | After a successful response |
| 1002 | `RequestFailed` | Warning | After a non-2xx response |
| 1003 | `StreamStarted` | Information | When an SSE stream opens |
| 1004 | `StreamChunkReceived` | Debug | For each SSE `data:` chunk |
| 1005 | `StreamCompleted` | Information | When a stream ends |

Structured properties on each entry include `HttpMethod`, `RequestUri`, `RequestBody`, `StatusCode`, `ResponseBody`, `ElapsedMilliseconds`, and (for streams) `ChunkIndex`, `ChunkCount`, `CompletionKind` (`done` or `end_of_stream`).

## Wiring it up

Pass an `ILoggerFactory` (or let DI inject one) to any provider client constructor. The DI extensions do this automatically:

```csharp
var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole());
services.AddOpenAiClient(opt => { opt.ApiKey = "..."; });
```

That's it. The console logger receives the structured properties; you can replace it with Serilog, NLog, OpenTelemetry's `AddOpenTelemetry()`, or anything else that plugs into `ILoggerFactory`. Cisharpai itself has no opinion on the sink.

## Trace correlation

If you wire OpenTelemetry into `ILoggerFactory` (or any sink that respects `Activity.Current`), the entries automatically pick up `TraceId` and `SpanId` from any ambient `System.Diagnostics.Activity`. To enable that in the built-in logger:

```csharp
services.AddLogging(b =>
{
    b.Configure(o => o.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
    b.AddSimpleConsole();
});
```

## Sample output

```
14:22:11 info: Cisharpai.LlmHttpClient[1000]
      Sending POST request to https://api.openai.com/v1/chat/completions with body {"model":"gpt-4.1-nano",...}
14:22:12 info: Cisharpai.LlmHttpClient[1001]
      POST request to https://api.openai.com/v1/chat/completions completed with status 200 in 612.4 ms and body {"id":"chatcmpl-...",...}
```

## Distributed tracing

Every HTTP call made by `LlmHttpClient` is wrapped in a `System.Diagnostics.Activity` (a span) created from a single `ActivitySource`. The source name is exposed as a public constant so consumers can subscribe correctly:

```csharp
public const string Cisharpai.CisharpaiTelemetry.ActivitySourceName = "Cisharpai";
```

If nobody is listening, the span is never allocated — `StartActivity` returns `null` and the cost is one boolean check per call. You only pay when you opt in.

### Span shape

| Property | Value |
|---|---|
| Source name | `Cisharpai` (from `CisharpaiTelemetry.ActivitySourceName`) |
| Span name | `"{HTTP method} {path}"` — e.g. `POST v1/chat/completions` |
| Kind | `ActivityKind.Client` |
| Status | `Ok` on success, `Error` on non-2xx or thrown exception |

Tags use the OpenTelemetry HTTP client semantic conventions:

| Tag | Set on |
|---|---|
| `http.request.method` | every span |
| `url.full` | every span (resolved against `HttpClient.BaseAddress`) |
| `server.address` | every span (host portion) |
| `http.response.status_code` | once the response status is known |

Streaming calls add three extra tags:

| Tag | Value |
|---|---|
| `cisharpai.stream` | `true` |
| `cisharpai.stream.chunks` | total `data:` lines emitted |
| `cisharpai.stream.completion_kind` | `done` (saw `[DONE]` sentinel), `end_of_stream` (provider closed naturally), or `incomplete` (caller broke early or an exception was thrown) |

### Subscribing

#### Option 1 — pure BCL listener (no NuGet dependency)

```csharp
using System.Diagnostics;
using Cisharpai;

ActivitySource.AddActivityListener(new ActivityListener
{
    ShouldListenTo = src => src.Name == CisharpaiTelemetry.ActivitySourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = a => Console.WriteLine($"{a.OperationName} {a.Status} {a.Duration}")
});
```

#### Option 2 — OpenTelemetry

```csharp
services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddSource(CisharpaiTelemetry.ActivitySourceName)
        .AddOtlpExporter());
```

#### Option 3 — do nothing

You still get `TraceId`/`SpanId` correlation on log entries that occur inside any *other* ambient `Activity` (for example an ASP.NET request span), as long as `ActivityTrackingOptions` is enabled on logging. The library never blocks correlation; it just doesn't generate spans of its own unless someone listens.
