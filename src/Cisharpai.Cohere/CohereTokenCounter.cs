using System.Text.Json;
using System.Text.Json.Serialization;
using Cisharpai.Cohere.Models;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Cohere;

/// <summary>
/// Token counter backed by Cohere's <c>POST /v1/tokenize</c> endpoint.
/// Constructed for a single model; the model name is baked in at creation.
/// </summary>
/// <remarks>
/// <para>
/// The Cohere tokenize API accepts text of 1–65,536 characters per request.
/// Text longer than 65,536 characters is split on whitespace boundaries and the per-chunk
/// token counts are summed. This sum is an <strong>upper-bound approximation</strong>: BPE merges
/// that would span the split point are lost, so the true count may be lower by a small number
/// of tokens. Prefer the local <c>TiktokenCounter</c> when exact counts matter and the model's
/// tokenizer is compatible.
/// </para>
/// </remarks>
public sealed class CohereTokenCounter : ITokenCounter
{
    internal const int MaxCharactersPerRequest = 65_536;
    private const string TokenizeEndpoint = "tokenize";

    private readonly LlmHttpClient _client;
    private readonly string _model;
    private readonly Uri _tokenizeBaseUri;

    public CohereTokenCounter(HttpClient httpClient, CohereClientOptions options, string model, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _client = new LlmHttpClient(httpClient, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }, loggerFactory?.CreateLogger<LlmHttpClient>());
        _model = model;
        _tokenizeBaseUri = DeriveV1BaseUri(options.BaseUrl);
    }

    public static CohereTokenCounter Create(
        IHttpMessageHandlerFactory handlerFactory,
        CohereClientOptions options,
        string model,
        string handlerName = "cisharpai",
        ILoggerFactory? loggerFactory = null)
    {
        var http = new HttpClient(new CohereAuthenticationHandler(options) { InnerHandler = handlerFactory.CreateHandler(handlerName) })
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromMinutes(2)
        };
        return new CohereTokenCounter(http, options, model, loggerFactory);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Text longer than 65,536 characters is split preferring whitespace boundaries and the per-chunk
    /// token counts are summed. The result is an upper-bound approximation because BPE merges
    /// across the split point are lost. If a chunk contains no whitespace within the limit,
    /// a hard cut at the character limit is used to honour the provider's per-request ceiling.
    /// </remarks>
    public ValueTask<int> CountAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
            return new ValueTask<int>(0);

        if (text.Length <= MaxCharactersPerRequest)
            return CountSingleAsync(text, cancellationToken);

        return CountChunkedAsync(text, cancellationToken);
    }

    private async ValueTask<int> CountSingleAsync(string text, CancellationToken cancellationToken)
    {
        var request = new CohereTokenizeRequest { Text = text, Model = _model };
        var tokenizeUrl = new Uri(_tokenizeBaseUri, TokenizeEndpoint).ToString();

        var response = await _client.PostAsync<CohereTokenizeRequest, CohereTokenizeResponse>(
            tokenizeUrl, request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Tokens.Length;
    }

    private async ValueTask<int> CountChunkedAsync(string text, CancellationToken cancellationToken)
    {
        var totalTokens = 0;
        var offset = 0;

        while (offset < text.Length)
        {
            var remaining = text.Length - offset;
            int chunkLength;

            if (remaining <= MaxCharactersPerRequest)
            {
                chunkLength = remaining;
            }
            else
            {
                chunkLength = FindWhitespaceSplitPoint(text, offset, MaxCharactersPerRequest);
            }

            var chunk = text.Substring(offset, chunkLength);
            totalTokens += await CountSingleAsync(chunk, cancellationToken).ConfigureAwait(false);
            offset += chunkLength;
        }

        return totalTokens;
    }

    internal static int FindWhitespaceSplitPoint(string text, int offset, int maxLength)
    {
        var searchEnd = offset + maxLength;
        for (var i = searchEnd - 1; i > offset; i--)
        {
            if (char.IsWhiteSpace(text[i]))
                return i - offset + 1;
        }
        return maxLength;
    }

    private static Uri DeriveV1BaseUri(string baseUrl)
    {
        var uri = new Uri(baseUrl, UriKind.Absolute);
        var path = uri.AbsolutePath;

        if (path.Contains("/v2/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
        {
            var newPath = path.Replace("/v2/", "/v1/", StringComparison.OrdinalIgnoreCase)
                              .Replace("/v2", "/v1", StringComparison.OrdinalIgnoreCase);
            if (!newPath.EndsWith('/'))
                newPath += '/';
            var builder = new UriBuilder(uri) { Path = newPath };
            return builder.Uri;
        }

        if (!path.EndsWith('/'))
        {
            var builder = new UriBuilder(uri) { Path = path + '/' };
            return builder.Uri;
        }

        return uri;
    }
}
