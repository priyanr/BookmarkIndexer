using BookmarkIndexer.Core.Modules.Auth;
using BookmarkIndexer.Shared.Types.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BookmarkIndexer.Web.Controllers;

/// <summary>The UI keeps the tokens and checks the OAuth state itself; these actions only talk to the provider, so they
/// run without a token.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(OAuthClient oauth, IOptions<AuthSettings> settings) : ControllerBase
{
    [HttpGet("status")]
    public AuthStatusDto Status()
    {
        return new AuthStatusDto(settings.Value.Enabled);
    }

    [HttpGet("login")]
    public IActionResult Login(string state)
    {
        return Redirect(oauth.AuthorizeUrl(state));
    }

    [HttpPost("exchange")]
    public async Task<ActionResult<TokenResponseDto>> Exchange(ExchangeCodeDto request)
    {
        var tokens = await oauth.ExchangeCode(request.Code);
        return tokens is null ? Unauthorized() : tokens;
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponseDto>> Refresh(RefreshTokenDto request)
    {
        var tokens = await oauth.Refresh(request.RefreshToken);
        return tokens is null ? Unauthorized() : tokens;
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenDto request)
    {
        await oauth.Revoke(request.RefreshToken);
        return NoContent();
    }
}
