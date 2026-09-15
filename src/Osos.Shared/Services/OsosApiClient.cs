using System.Net.Http.Headers;
using System.Net.Http.Json;
using Osos.Contracts;

namespace Osos.Shared.Services;

/// <summary>Osos.Server backend'ini çağıran tipli istemci. Tüm platformlar bunu kullanır.</summary>
public sealed class OsosApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenStore _tokens;

    public string? Username { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public event Action? AuthChanged;

    public OsosApiClient(HttpClient http, ITokenStore tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    /// <summary>Uygulama açılışında saklı token'ı yükler.</summary>
    public async Task InitializeAsync()
    {
        var token = await _tokens.GetAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            IsAuthenticated = true;
            AuthChanged?.Invoke();
        }
    }

    // ---- Kimlik ----
    public Task<AuthResponse?> RegisterAsync(RegisterRequest req) => AuthPost("api/auth/register", req);
    public Task<AuthResponse?> LoginAsync(AppLoginRequest req) => AuthPost("api/auth/login", req);

    public async Task LogoutAsync()
    {
        await _tokens.ClearAsync();
        _http.DefaultRequestHeaders.Authorization = null;
        IsAuthenticated = false;
        Username = null;
        AuthChanged?.Invoke();
    }

    private async Task<AuthResponse?> AuthPost(string path, object req)
    {
        var resp = await _http.PostAsJsonAsync(path, req);
        if (!resp.IsSuccessStatusCode) return null;
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth is not null)
        {
            await _tokens.SetAsync(auth.Token);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            IsAuthenticated = true;
            Username = auth.Username;
            AuthChanged?.Invoke();
        }
        return auth;
    }

    // ---- OSOS ----
    public async Task<OsosLinkResponse?> LinkOsosAsync(OsosLinkRequest req)
        => await (await _http.PostAsJsonAsync("api/osos/link", req)).Content.ReadFromJsonAsync<OsosLinkResponse>();

    public async Task<MeDto?> GetMeAsync()
    {
        var resp = await _http.GetAsync("api/osos/me");
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<MeDto>() : null;
    }

    public Task<OsosResult?> ConsumptionAsync(ConsumptionQuery q) => Post<ConsumptionQuery, OsosResult>("api/osos/consumption", q);
    public Task<OsosResult?> EndexAsync(EndexQuery q) => Post<EndexQuery, OsosResult>("api/osos/endex", q);
    public Task<OsosResult?> ProfilesAsync(ProfilesQuery q) => Post<ProfilesQuery, OsosResult>("api/osos/profiles", q);
    public Task<OsosResult?> SubscriptionsAsync(SubscriptionsQuery q) => Post<SubscriptionsQuery, OsosResult>("api/osos/subscriptions", q);
    public Task<OsosResult?> OwnerConsumptionsAsync(OwnerConsumptionsQuery q) => Post<OwnerConsumptionsQuery, OsosResult>("api/osos/dashboard/owner-consumptions", q);

    // ---- Arama geçmişi ----
    public async Task<PagedResult<SearchHistoryDto>?> GetHistoryAsync(int page = 1, int pageSize = 25)
        => await _http.GetFromJsonAsync<PagedResult<SearchHistoryDto>>($"api/searches?page={page}&pageSize={pageSize}");

    public async Task<SearchResultDto?> GetSnapshotAsync(long id)
    {
        var resp = await _http.GetAsync($"api/searches/{id}");
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SearchResultDto>() : null;
    }

    public Task<OsosResult?> RerunAsync(long id) => Post<object?, OsosResult>($"api/searches/{id}/rerun", null);
    public async Task<bool> DeleteAsync(long id) => (await _http.DeleteAsync($"api/searches/{id}")).IsSuccessStatusCode;

    /// <summary>Kayıtlı aramanın CSV'sini indirir (dosya + adı).</summary>
    public async Task<(byte[] bytes, string fileName)?> ExportCsvAsync(long id)
    {
        var resp = await _http.GetAsync($"api/searches/{id}/export");
        if (!resp.IsSuccessStatusCode) return null;
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var name = resp.Content.Headers.ContentDisposition?.FileNameStar
                   ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                   ?? $"arama_{id}.csv";
        return (bytes, name);
    }

    private async Task<TOut?> Post<TIn, TOut>(string path, TIn body)
    {
        var resp = await _http.PostAsJsonAsync(path, body);
        if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<TOut>();
    }
}
