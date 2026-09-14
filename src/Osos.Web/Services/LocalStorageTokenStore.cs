using Microsoft.JSInterop;
using Osos.Shared.Services;

namespace Osos.Web.Services;

/// <summary>WASM için localStorage tabanlı token deposu.</summary>
public sealed class LocalStorageTokenStore : ITokenStore
{
    private const string Key = "osos_token";
    private readonly IJSRuntime _js;
    public LocalStorageTokenStore(IJSRuntime js) => _js = js;

    public async Task<string?> GetAsync()
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", Key); }
        catch { return null; }
    }

    public async Task SetAsync(string token) => await _js.InvokeVoidAsync("localStorage.setItem", Key, token);
    public async Task ClearAsync() => await _js.InvokeVoidAsync("localStorage.removeItem", Key);
}
