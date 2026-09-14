using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Osos.Core.Osos;
using Osos.Server.Data;

namespace Osos.Server.Services;

/// <summary>
/// Uygulama kullanıcısı başına OSOS oturumunu yönetir: giriş, SessionKey + cookie cache,
/// ve MethodName çağrıları. OSOS şifresi DataProtection ile korunur.
/// </summary>
public sealed class OsosSessionService
{
    private sealed class Session
    {
        public required string SessionKey { get; init; }
        public required OsosClient Client { get; init; }
        public required HttpClient Http { get; init; }
        public long CustomerSerno { get; init; }
        public string? SubscriptionsJson { get; init; }
        public DateTime EstablishedAt { get; init; } = DateTime.UtcNow;
    }

    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(20);
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDataProtector _protector;

    public OsosSessionService(IServiceScopeFactory scopeFactory, IDataProtectionProvider dp)
    {
        _scopeFactory = scopeFactory;
        _protector = dp.CreateProtector("Osos.OsosPassword");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string cipher) => _protector.Unprotect(cipher);

    /// <summary>Verilen kimlikle OSOS'a giriş yapılabiliyor mu (bağlama/doğrulama için). Başarılıysa müşteri Serno'sunu da döndürür.</summary>
    public async Task<(bool ok, string? message, long serno)> TryLoginAsync(string userCode, string password, bool rememberMe, CancellationToken ct)
    {
        var (client, http) = CreateClient();
        try
        {
            var resp = await client.LoginAsync(userCode, password, rememberMe: rememberMe, ct: ct);
            if (string.IsNullOrWhiteSpace(resp.SessionKey))
                return (false, resp.Message ?? "SessionKey alınamadı (captcha zorunlu olabilir).", 0);
            return (true, null, resp.Serno);
        }
        catch (Exception ex) { return (false, ex.Message, 0); }
        finally { http.Dispose(); }
    }

    /// <summary>Kullanıcı için geçerli bir oturum sağlar (gerekirse yeniden login).</summary>
    public async Task<string> EnsureSessionAsync(string appUserId, CancellationToken ct)
    {
        if (_sessions.TryGetValue(appUserId, out var s) && DateTime.UtcNow - s.EstablishedAt < SessionTtl)
            return s.SessionKey;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cred = await db.OsosCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.AppUserId == appUserId, ct)
                   ?? throw new InvalidOperationException("OSOS hesabı bağlı değil. Önce /api/osos/link çağırın.");

        string password = Unprotect(cred.OsosPasswordProtected);
        var (client, http) = CreateClient();
        var resp = await client.LoginAsync(cred.OsosUserCode, password, rememberMe: cred.RememberMe, ct: ct);
        if (string.IsNullOrWhiteSpace(resp.SessionKey))
        {
            http.Dispose();
            throw new InvalidOperationException(resp.Message ?? "OSOS login başarısız (SessionKey yok).");
        }

        // eski oturumu at
        if (_sessions.TryRemove(appUserId, out var old)) old.Http.Dispose();
        _sessions[appUserId] = new Session
        {
            SessionKey = resp.SessionKey!,
            Client = client,
            Http = http,
            CustomerSerno = resp.Serno,
            SubscriptionsJson = resp.Subscriptions?.GetRawText()
        };
        return resp.SessionKey!;
    }

    /// <summary>Kullanıcının müşteri Serno'sunu döndürür (gerekirse oturum kurar).</summary>
    public async Task<long> GetCustomerSernoAsync(string appUserId, CancellationToken ct)
    {
        await EnsureSessionAsync(appUserId, ct);
        return _sessions[appUserId].CustomerSerno;
    }

    /// <summary>Kullanıcının tesisat/abone listesini (ham JSON) döndürür.</summary>
    public async Task<(long serno, string? subscriptionsJson)> GetProfileAsync(string appUserId, CancellationToken ct)
    {
        await EnsureSessionAsync(appUserId, ct);
        var s = _sessions[appUserId];
        return (s.CustomerSerno, s.SubscriptionsJson);
    }

    /// <summary>Kullanıcının oturumuyla bir MethodName çağırır; çözülmüş JSON döner.</summary>
    public async Task<string> CallAsync(string appUserId, string methodName, object? parameters, CancellationToken ct)
    {
        string sessionKey = await EnsureSessionAsync(appUserId, ct);
        var session = _sessions[appUserId];
        return await session.Client.CallAsync(sessionKey, methodName, parameters, ct);
    }

    private static (OsosClient, HttpClient) CreateClient()
    {
        var cookies = new CookieContainer();
        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = cookies, AllowAutoRedirect = true };
        var http = new HttpClient(handler) { BaseAddress = new Uri(OsosClient.DefaultBaseUrl) };
        http.DefaultRequestHeaders.Add("Accept", "application/json");
        return (new OsosClient(http), http);
    }
}
