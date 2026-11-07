namespace BookmarkIndexer.Shared.Types.Auth;

/// <summary>ExpiresIn: seconds until the access token expires.</summary>
public record TokenResponseDto(string AccessToken, string RefreshToken, int ExpiresIn);
