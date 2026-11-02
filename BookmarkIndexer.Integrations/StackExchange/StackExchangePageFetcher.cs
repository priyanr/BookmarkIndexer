using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookmarkIndexer.Shared;

namespace BookmarkIndexer.Integrations.StackExchange;

/// <summary>
/// Loads Stack Exchange questions from the Stack Exchange API and passes every other URL to the browser.
/// Cloudflare answers the headless browser with a challenge it can't pass, so these sites fail in Lightpanda.
/// </summary>
public class StackExchangePageFetcher(HttpClient http, IPageFetcher otherSites) : IPageFetcher
{
    const string ApiUrl = "https://api.stackexchange.com/2.3/";
    // Made once with /2.3/filters/create?include=question.body;question.answers;answer.body (filters never expire),
    // so one request returns the question with all its answers.
    const string Filter = "!)RL-JZPFNmXHg95AslvrTXdy";
    const string UserAgent = "BookmarkIndexer/1.0";

    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    static readonly Regex QuestionUrl = new(@"^https?://(?:www\.)?(?<site>(?:[a-z0-9-]+\.)?(?:stackoverflow\.com|stackexchange\.com|serverfault\.com|superuser\.com|askubuntu\.com|mathoverflow\.net|stackapps\.com))/questions/(?<id>\d+)", RegexOptions.IgnoreCase);

    public async Task<string> FetchHtml(string url, CancellationToken ct)
    {
        var match = QuestionUrl.Match(url);
        if (!match.Success)
        {
            return await otherSites.FetchHtml(url, ct);
        }
        var site = match.Groups["site"].Value.ToLowerInvariant();
        var id = match.Groups["id"].Value;

        // Without an app key the API allows 300 requests a day per IP. Over it, the error says when it resets.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}questions/{id}?site={site}&filter={Uri.EscapeDataString(Filter)}");
        // Cloudflare in front of the API answers 403 with an HTML page to requests without a User-Agent.
        request.Headers.UserAgent.ParseAdd(UserAgent);
        using var response = await http.SendAsync(request, ct);
        if (response.Content.Headers.ContentType?.MediaType != "application/json")
        {
            throw new HttpRequestException($"Stack Exchange API answered HTTP {(int)response.StatusCode} without JSON.");
        }
        var body = await response.Content.ReadFromJsonAsync<QuestionsResponse>(Json, ct);
        if (body?.ErrorName != null)
        {
            throw new HttpRequestException($"Stack Exchange API {body.ErrorName}: {body.ErrorMessage}");
        }
        response.EnsureSuccessStatusCode();
        var question = body?.Items?.FirstOrDefault()
            ?? throw new HttpRequestException($"Stack Exchange has no question {id} on {site}; it was probably deleted.");

        // Title and bodies come HTML-encoded from the API's safe filter, so they go into the page as they are.
        var answers = string.Concat((question.Answers ?? []).OrderByDescending(a => a.Score).Select(a => $"<section>{a.Body}</section>"));
        return $"<html><head><title>{question.Title}</title></head><body><article><h1>{question.Title}</h1>{question.Body}{answers}</article></body></html>";
    }

    private sealed record QuestionsResponse(List<Question>? Items, string? ErrorName, string? ErrorMessage);

    private sealed record Question(string Title, string Body, List<Answer>? Answers);

    private sealed record Answer(string Body, int Score);
}
