using System.Net;
using System.Text;
using BookmarkIndexer.Core.Modules.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace BookmarkIndexer.Tests.Auth;

public class OAuthClientTests
{
    static readonly AuthSettings Settings = new()
    {
        Enabled = true,
        Authority = "https://mws.priyan.in",
        ClientId = "BookmarkIndexer_1",
        ClientSecret = "s3cret",
        RedirectUri = "https://apps.priyan.in/bm/auth/callback",
        AuthorizationEndpoint = "/oauth2/authorize",
        TokenEndpoint = "/oauth2/token",
        RevocationEndpoint = "/oauth2/revoke",
        IntrospectionEndpoint = "/oauth2/validate",
    };

    static OAuthClient Client(FakeHandler handler)
    {
        return new OAuthClient(new HttpClient(handler), Options.Create(Settings));
    }

    [Fact]
    public void Login_url_points_at_the_providers_authorize_endpoint()
    {
        var url = Client(new FakeHandler(HttpStatusCode.OK, "{}")).AuthorizeUrl("abc 1");

        Assert.Equal("https://mws.priyan.in/oauth2/authorize?client_id=BookmarkIndexer_1"
            + "&redirect_uri=https%3A%2F%2Fapps.priyan.in%2Fbm%2Fauth%2Fcallback&response_type=code&state=abc%201", url);
    }

    [Fact]
    public async Task Exchanges_a_code_for_tokens_with_the_client_credentials()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"access_token":"a.b.c","token_type":"Bearer","expires_in":300,"refresh_token":"r1"}""");

        var tokens = await Client(handler).ExchangeCode("code1");

        Assert.NotNull(tokens);
        Assert.Equal(("a.b.c", "r1", 300), (tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn));
        var (uri, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://mws.priyan.in/oauth2/token", uri);
        Assert.Equal("grant_type=authorization_code&code=code1&redirect_uri=https%3A%2F%2Fapps.priyan.in%2Fbm%2Fauth%2Fcallback"
            + "&client_id=BookmarkIndexer_1&client_secret=s3cret", body);
    }

    [Fact]
    public async Task A_rejected_refresh_token_returns_null()
    {
        var tokens = await Client(new FakeHandler(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")).Refresh("old");

        Assert.Null(tokens);
    }

    [Fact]
    public async Task A_provider_error_throws_instead_of_logging_the_user_out()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(new FakeHandler(HttpStatusCode.BadGateway, "")).Refresh("old"));
    }

    [Theory]
    [InlineData("""{"active":true,"client_id":"BookmarkIndexer_1","sub":"priyan"}""", true)]
    [InlineData("""{"active":true,"client_id":"PersonalAI_2","sub":"priyan"}""", false)]
    [InlineData("""{"active":false}""", false)]
    public async Task A_token_is_active_only_when_the_provider_says_so_for_this_client(string json, bool expected)
    {
        var handler = new FakeHandler(HttpStatusCode.OK, json);

        var active = await Client(handler).IsActive("a.b.c", CancellationToken.None);

        Assert.Equal(expected, active);
        Assert.Equal(("https://mws.priyan.in/oauth2/validate", "token=a.b.c"), Assert.Single(handler.Requests));
    }

    sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(string Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.ToString(), await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
