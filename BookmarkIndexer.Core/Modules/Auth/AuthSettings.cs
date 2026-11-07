namespace BookmarkIndexer.Core.Modules.Auth;

/// <summary>The "Auth" config section. Login goes to an OAuth2 provider without discovery (mws.priyan.in), so its
/// endpoints are listed here, as absolute URLs or paths relative to Authority. JwtKey is the provider's shared HMAC key
/// that signs its access tokens.</summary>
public class AuthSettings
{
    public bool Enabled { get; set; }
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string Scope { get; set; } = "";
    public string JwtKey { get; set; } = "";
    public string AuthorizationEndpoint { get; set; } = "";
    public string TokenEndpoint { get; set; } = "";
    public string RevocationEndpoint { get; set; } = "";
    public string IntrospectionEndpoint { get; set; } = "";
}
