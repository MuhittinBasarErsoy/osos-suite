namespace Osos.Shared.Services;

/// <summary>JWT token'ını platforma göre saklar (web: localStorage, MAUI: SecureStorage).</summary>
public interface ITokenStore
{
    Task<string?> GetAsync();
    Task SetAsync(string token);
    Task ClearAsync();
}

/// <summary>Varsayılan bellek içi saklama (fallback).</summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private string? _token;
    public Task<string?> GetAsync() => Task.FromResult(_token);
    public Task SetAsync(string token) { _token = token; return Task.CompletedTask; }
    public Task ClearAsync() { _token = null; return Task.CompletedTask; }
}
