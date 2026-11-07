using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BookmarkIndexer.Shared.Types.Auth;
using Microsoft.Extensions.Options;

namespace BookmarkIndexer.Core.Modules.Auth;

/// <summary>Authorization Code flow against the OAuth2 provider in <see cref="AuthSettings"/>. A 4xx from the provider
/// means the code or token was rejected and comes back as null (the user must log in again); any other failure throws
/// HttpRequestException, which Program.cs answers with 503 so a provider outage doesn't log the user out.</summary>
public class OAuthClient(HttpClient http, IOptions<AuthSettings> options)
{
    public string AuthorizeUrl(string state)
    {
        var settings = options.Value;
        var query = new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = settings.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = settings.Scope,
            ["state"] = state,
        };
        return Endpoint(settings.AuthorizationEndpoint) + "?"
            + string.Join("&", query.Where(p => p.Value.Length > 0).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
    }

    public async Task<TokenResponseDto?> ExchangeCode(string code)
    {
        return await RequestTokens(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = options.Value.RedirectUri,
        });
    }

    public async Task<TokenResponseDto?> Refresh(string refreshToken)
    {
        return await RequestTokens(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        });
    }

    /// <summary>The signature alone can't show that a token was revoked or that it was issued to another client of the
    /// same provider, so every request asks the provider (RFC 7662 introspection).</summary>
    public async Task<bool> IsActive(string accessToken, CancellationToken ct)
    {
        using var response = await http.PostAsync(Endpoint(options.Value.IntrospectionEndpoint),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = accessToken }), ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Introspection>(ct);
        return result is { Active: true } && result.ClientId == options.Value.ClientId;
    }

    public async Task Revoke(string refreshToken)
    {
        using var response = await http.PostAsync(Endpoint(options.Value.RevocationEndpoint),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken }));
        response.EnsureSuccessStatusCode();
    }

    async Task<TokenResponseDto?> RequestTokens(Dictionary<string, string> form)
    {
        form["client_id"] = options.Value.ClientId;
        form["client_secret"] = options.Value.ClientSecret;
        using var response = await http.PostAsync(Endpoint(options.Value.TokenEndpoint), new FormUrlEncodedContent(form));
        if ((int)response.StatusCode is >= 400 and < 500)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<Tokens>()
            ?? throw new HttpRequestException("The token endpoint returned an empty response.");
        return new TokenResponseDto(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn);
    }

    string Endpoint(string pathOrUrl)
    {
        return new Uri(new Uri(options.Value.Authority), pathOrUrl).ToString();
    }

    sealed record Tokens(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    sealed record Introspection(
        [property: JsonPropertyName("active")] bool Active,
        [property: JsonPropertyName("client_id")] string? ClientId);
}
