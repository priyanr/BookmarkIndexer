namespace BookmarkIndexer.Shared.Types.Auth;

/// <summary>The authorization code the provider sent back to the UI's /auth/callback.</summary>
public record ExchangeCodeDto(string Code);
