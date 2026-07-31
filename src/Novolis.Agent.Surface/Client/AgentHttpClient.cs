using System.Net.Http.Json;
using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>HTTP client for <see cref="AgentHttpHost"/> (agent / MCP sidecar / scripts).</summary>
public sealed class AgentHttpClient : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public AgentHttpClient(string baseUrl, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        BaseUrl = baseUrl.TrimEnd('/');
        if (httpClient is null)
        {
            _http = new HttpClient { BaseAddress = new Uri(BaseUrl + "/") };
            _ownsClient = true;
        }
        else
        {
            _http = httpClient;
            _ownsClient = false;
        }
    }

    public string BaseUrl { get; }

    public static AgentHttpClient? TryFromDefinition(AgentSurfaceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var env = Environment.GetEnvironmentVariable(definition.HttpEnableEnv + "_URL");
        var url = !string.IsNullOrWhiteSpace(env) ? env.Trim() : definition.TryReadHttpBaseUrl();
        if (string.IsNullOrWhiteSpace(url))
            url = $"http://127.0.0.1:{definition.DefaultHttpPort}";
        return new AgentHttpClient(url);
    }

    public Task<AgentHello> HelloAsync(CancellationToken cancellationToken = default) =>
        GetResultAsync<AgentHello>("agent/hello", cancellationToken);

    public Task<AgentSnapshot> SnapshotAsync(CancellationToken cancellationToken = default) =>
        GetResultAsync<AgentSnapshot>("agent/snapshot", cancellationToken);

    public Task<AgentActionsResponse> ActionsAsync(CancellationToken cancellationToken = default) =>
        GetResultAsync<AgentActionsResponse>("agent/actions", cancellationToken);

    public Task<JsonElement> DocumentAsync(CancellationToken cancellationToken = default) =>
        GetResultAsync<JsonElement>("agent/document", cancellationToken);

    public Task<AgentAnnouncement> AnnounceAsync(CancellationToken cancellationToken = default) =>
        GetResultAsync<AgentAnnouncement>("agent/announce", cancellationToken);

    public Task<AgentCommandResult> ContinueAsync(CancellationToken cancellationToken = default) =>
        PostResultAsync<AgentCommandResult>("agent/continue", null, cancellationToken);

    public Task<AgentSubscribeResponse> SubscribeAsync(CancellationToken cancellationToken = default) =>
        PostResultAsync<AgentSubscribeResponse>("agent/subscribe", null, cancellationToken);

    public Task<AgentCommandResult> CommandAsync(AgentCommand command, CancellationToken cancellationToken = default) =>
        PostResultAsync<AgentCommandResult>("agent/command", command, cancellationToken);

    private async Task<T> GetResultAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await UnwrapAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> PostResultAsync<T>(string path, object? body, CancellationToken cancellationToken)
    {
        using var response = body is null
            ? await _http.PostAsync(path, content: null, cancellationToken).ConfigureAwait(false)
            : await _http.PostAsJsonAsync(path, body, AgentJson.Options, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await UnwrapAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> UnwrapAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "request failed";
            throw new InvalidOperationException(err);
        }

        if (!doc.RootElement.TryGetProperty("result", out var result))
            throw new InvalidOperationException("Response missing result.");

        return result.Deserialize<T>(AgentJson.Options)
               ?? throw new InvalidOperationException("Failed to deserialize result.");
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _http.Dispose();
        return ValueTask.CompletedTask;
    }
}
