using System.Net;
using System.Text;
using BookmarkIndexer.Core.Modules.Indexing;
using BookmarkIndexer.Integrations.StackExchange;
using BookmarkIndexer.Shared;
using Xunit;

namespace BookmarkIndexer.Tests.Integrations.StackExchange;

public class StackExchangePageFetcherTests
{
    // Trimmed from a real response of /2.3/questions/62540512?site=stackoverflow.com with the fetcher's filter.
    const string QuestionJson = """
        {"items":[{"tags":["kubernetes"],"answers":[
          {"is_accepted":false,"score":2,"answer_id":62571800,"question_id":62540512,"body":"<p>Check that kube-proxy runs on every node &amp; that its iptables rules exist.</p>"},
          {"is_accepted":true,"score":5,"answer_id":62571725,"question_id":62540512,"body":"<p>The flannel network overlapped the cri-o bridge subnet 10.85.0.0/16. Remove /etc/cni/net.d/100-crio-bridge.conf and restart the kubelet.</p>"}],
          "answer_count":2,"score":0,"question_id":62540512,"link":"https://stackoverflow.com/questions/62540512/k8s-1-18-1-api-not-reachable-since-update-to-1-18-1",
          "title":"k8s 1.18.1: api not reachable since update to 1.18.1",
          "body":"<p>I've updated my cluster to v1.18.1. Some applications have API access but they returned an error that the API is not reachable.</p>\n<pre><code>dial tcp 10.96.0.1:443: i/o timeout\n</code></pre>\n<p>As network plugin I use flannel.</p>"}],
         "has_more":false,"quota_max":300,"quota_remaining":298}
        """;

    const string ThrottleJson = """
        {"error_id":502,"error_message":"too many requests from this IP, more requests available in 80000 seconds","error_name":"throttle_violation"}
        """;

    [Fact]
    public async Task Loads_a_question_and_its_answers_from_the_api()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, QuestionJson);
        var fetcher = new StackExchangePageFetcher(new HttpClient(handler), new FakeOtherSites());

        var html = await fetcher.FetchHtml("https://stackoverflow.com/questions/62540512/k8s-1-18-1-api-not-reachable-since-update-to-1-18-1", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("https://api.stackexchange.com/2.3/questions/62540512?site=stackoverflow.com&filter=", request.RequestUri!.ToString());
        // The API refuses requests without a User-Agent.
        Assert.Equal("BookmarkIndexer/1.0", request.Headers.UserAgent.ToString());
        var text = PageText.Extract("https://stackoverflow.com/questions/62540512", html);
        Assert.Contains("Some applications have API access", text);
        Assert.Contains("As network plugin I use flannel.", text);
        Assert.Contains("Remove /etc/cni/net.d/100-crio-bridge.conf", text);
        Assert.Contains("kube-proxy runs on every node & that its iptables rules exist.", text);
        // The highest-scored answer comes first, as on the site.
        Assert.True(text.IndexOf("cri-o bridge", StringComparison.Ordinal) < text.IndexOf("kube-proxy", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http://serverfault.com/questions/123/some-title", "serverfault.com", "123")]
    [InlineData("https://dba.stackexchange.com/questions/45/x?noredirect=1", "dba.stackexchange.com", "45")]
    [InlineData("https://www.superuser.com/questions/7#answer-9", "superuser.com", "7")]
    public async Task Asks_the_api_for_the_site_and_question_in_the_url(string url, string site, string id)
    {
        var handler = new FakeHandler(HttpStatusCode.OK, QuestionJson);
        var fetcher = new StackExchangePageFetcher(new HttpClient(handler), new FakeOtherSites());

        await fetcher.FetchHtml(url, CancellationToken.None);

        Assert.StartsWith($"https://api.stackexchange.com/2.3/questions/{id}?site={site}&", Assert.Single(handler.RequestUris).ToString());
    }

    [Theory]
    [InlineData("https://example.com/questions/123/not-stack-exchange")]
    [InlineData("https://stackoverflow.com/users/22656/jon-skeet")]
    [InlineData("https://notstackoverflow.com/questions/1/x")]
    public async Task Passes_other_urls_to_the_browser(string url)
    {
        var handler = new FakeHandler(HttpStatusCode.OK, QuestionJson);
        var otherSites = new FakeOtherSites();
        var fetcher = new StackExchangePageFetcher(new HttpClient(handler), otherSites);

        var html = await fetcher.FetchHtml(url, CancellationToken.None);

        Assert.Equal("<html>from the browser</html>", html);
        Assert.Equal([url], otherSites.FetchedUrls);
        Assert.Empty(handler.RequestUris);
    }

    [Fact]
    public async Task Reports_the_api_error_message()
    {
        var fetcher = new StackExchangePageFetcher(new HttpClient(new FakeHandler(HttpStatusCode.BadRequest, ThrottleJson)), new FakeOtherSites());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            fetcher.FetchHtml("https://stackoverflow.com/questions/62540512/x", CancellationToken.None));

        Assert.Equal("Stack Exchange API throttle_violation: too many requests from this IP, more requests available in 80000 seconds", error.Message);
    }

    [Fact]
    public async Task Fails_when_the_question_no_longer_exists()
    {
        var fetcher = new StackExchangePageFetcher(
            new HttpClient(new FakeHandler(HttpStatusCode.OK, """{"items":[],"has_more":false,"quota_max":300,"quota_remaining":297}""")),
            new FakeOtherSites());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            fetcher.FetchHtml("https://superuser.com/questions/999/x", CancellationToken.None));

        Assert.Equal("Stack Exchange has no question 999 on superuser.com; it was probably deleted.", error.Message);
    }

    [Fact]
    public async Task Reports_an_answer_that_is_not_json()
    {
        var handler = new FakeHandler(HttpStatusCode.Forbidden, "<!DOCTYPE html><title>Just a moment...</title>", "text/html");
        var fetcher = new StackExchangePageFetcher(new HttpClient(handler), new FakeOtherSites());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            fetcher.FetchHtml("https://stackoverflow.com/questions/62540512/x", CancellationToken.None));

        Assert.Equal("Stack Exchange API answered HTTP 403 without JSON.", error.Message);
    }

    sealed class FakeHandler(HttpStatusCode status, string body, string mediaType = "application/json") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public IEnumerable<Uri> RequestUris => Requests.Select(r => r.RequestUri!);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
        }
    }

    sealed class FakeOtherSites : IPageFetcher
    {
        public List<string> FetchedUrls { get; } = [];

        public Task<string> FetchHtml(string url, CancellationToken ct)
        {
            FetchedUrls.Add(url);
            return Task.FromResult("<html>from the browser</html>");
        }
    }
}
