using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Shell;

/// <summary>
/// The newest published release of <see cref="UpdateRules.Repository"/>, read from the GitHub REST API without signing in
/// (<c>GET /repos/{owner}/{repo}/releases/latest</c>: drafts and pre-releases are never "latest"). GitHub refuses a request without a
/// User-Agent, so one is sent. Nothing about the user is sent. Every failure comes back as a detail for the log, never an exception.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed, IDisposable
{
    private static readonly Uri _latest = new($"https://api.github.com/repos/{UpdateRules.Repository}/releases/latest");

    private readonly HttpClient _http;

    public GitHubReleaseFeed(string userAgent, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<ReleaseFeedResult> GetLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(_latest, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new ReleaseFeedResult(null, "no release is published");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ReleaseFeedResult(null, $"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("tag_name", out var tag)
                && tag.ValueKind == JsonValueKind.String
                ? new ReleaseFeedResult(tag.GetString(), null)
                : new ReleaseFeedResult(null, "the answer has no tag_name");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            // No network, a proxy, a timeout (TaskCanceledException without our token), a cut answer.
            return new ReleaseFeedResult(null, exception.Message);
        }
    }

    public void Dispose() => _http.Dispose();
}
