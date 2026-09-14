using System.Text;
using System.Text.Json;
using Osos.Core.Crypto;

namespace Osos.Core.Osos;

/// <summary>
/// OSOS portal backend'i (aril-portalserver) ile şifreli konuşan istemci.
/// İstek gövdeleri OsosCrypto ile şifrelenir, yanıtlar çözülür.
/// </summary>
public sealed class OsosClient
{
    public const string DefaultBaseUrl = "https://osos.uedas.com.tr/aril-portalserver/api/";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public OsosClient(HttpClient http)
    {
        _http = http;
        _http.BaseAddress ??= new Uri(DefaultBaseUrl);

        // Tarayıcı benzeri başlıklar — bazı kurumsal backend/WAF'lar User-Agent/Origin/Referer bekler.
        var h = _http.DefaultRequestHeaders;
        if (h.UserAgent.Count == 0)
            h.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/152.0.0.0 Safari/537.36");
        if (!h.Contains("Origin")) h.Add("Origin", "https://osos.uedas.com.tr");
        if (h.Referrer is null) h.Referrer = new Uri("https://osos.uedas.com.tr/");
        if (!h.Accept.Any()) h.Accept.ParseAdd("application/json");
    }

    /// <summary>
    /// Kullanıcı adı/şifre ile giriş yapar; başarılıysa SessionKey döner.
    /// captchaToken sunucuda zorunlu değil (bkz. analiz); yine de opsiyonel geçilebilir.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(string userCode, string password,
        string? captchaToken = null, bool rememberMe = false, CancellationToken ct = default)
    {
        var req = new LoginRequest
        {
            UserCode = userCode,
            Password = password,
            LoginType = LoginTypes.CustomerPortal,
            RememberMe = rememberMe,
            Token = null,                          // ilk giriş: auth-phase token yok
            LoginPhase = LoginPhases.TokenGenerate,
            GRecaptchaResponse = string.IsNullOrEmpty(captchaToken) ? null : captchaToken  // boşsa gönderilmez
        };

        // Tarayıcı gibi: login'den önce oturum çerezini al (bazı sunucular bunu bekler).
        try { await _http.GetAsync("checklogin", ct); } catch { /* cookie warm-up; hatayı yut */ }

        string plain = JsonSerializer.Serialize(req, JsonOpts);
        string decrypted = await PostEncryptedAsync("login", plain, OsosCrypto.ConstPassPhrase, OsosCrypto.ConstPassPhrase, ct);
        return JsonSerializer.Deserialize<LoginResponse>(decrypted, JsonOpts) ?? new LoginResponse();
    }

    /// <summary>Oturumun hâlâ geçerli olup olmadığını kontrol eder.</summary>
    public async Task<bool> CheckLoginAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("checklogin", ct);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>
    /// customer-esb gateway'ine bir MethodName çağrısı yapar ve çözülmüş JSON metnini döndürür.
    /// </summary>
    public async Task<string> CallAsync(string sessionKey, string methodName, object? parameters,
        CancellationToken ct = default)
    {
        var envelope = new EsbEnvelope { MethodName = methodName, Parameters = parameters };
        string plain = JsonSerializer.Serialize(envelope, JsonOpts);
        return await PostEncryptedAsync("customer-esb", plain, sessionKey, sessionKey, ct);
    }

    /// <summary>Çağrıyı yapıp yanıtı JsonDocument olarak döndürür.</summary>
    public async Task<JsonDocument> CallJsonAsync(string sessionKey, string methodName, object? parameters,
        CancellationToken ct = default)
    {
        string json = await CallAsync(sessionKey, methodName, parameters, ct);
        return JsonDocument.Parse(json);
    }

    private async Task<string> PostEncryptedAsync(string path, string plaintext,
        string encryptPassPhrase, string decryptPassPhrase, CancellationToken ct)
    {
        string encryptedBody = OsosCrypto.Encrypt(plaintext, encryptPassPhrase);
        using var content = new StringContent(encryptedBody, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(path, content, ct);

        string respText = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            string detail = respText.Length > 300 ? respText[..300] : respText;
            throw new HttpRequestException($"OSOS {path} → {(int)resp.StatusCode}. Yanıt: {detail}");
        }
        if (string.IsNullOrWhiteSpace(respText)) return "{}";

        // Yanıt şifreliyse çöz; değilse (nadiren düz JSON) olduğu gibi döndür.
        string? decrypted = OsosCrypto.Decrypt(respText.Trim('"'), decryptPassPhrase);
        return decrypted ?? respText;
    }
}
