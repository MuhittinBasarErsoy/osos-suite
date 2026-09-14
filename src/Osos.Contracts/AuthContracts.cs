namespace Osos.Contracts;

public sealed record RegisterRequest(string Username, string Email, string Password);

public sealed record AppLoginRequest(string Username, string Password);

public sealed record AuthResponse(string Token, DateTime ExpiresAt, string Username);

/// <summary>Uygulama kullanıcısına OSOS hesabını bağlar / doğrular.</summary>
public sealed record OsosLinkRequest(string OsosUserCode, string OsosPassword, bool RememberMe = true);

public sealed record OsosLinkResponse(bool Success, string? Message);
