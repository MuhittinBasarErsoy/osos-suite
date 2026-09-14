using Osos.Shared.Services;

namespace Osos.Maui.Services;

/// <summary>MAUI için SecureStorage tabanlı token deposu (Android/Windows).</summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string Key = "osos_token";

    public async Task<string?> GetAsync()
    {
        try { return await SecureStorage.Default.GetAsync(Key); }
        catch { return null; }
    }

    public Task SetAsync(string token) => SecureStorage.Default.SetAsync(Key, token);

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
