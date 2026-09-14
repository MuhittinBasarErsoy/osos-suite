using System.Text.Json;
using System.Text.Json.Serialization;

namespace Osos.Core.Osos;

/// <summary>customer-esb / supplier-esb gateway'ine gönderilen RPC zarfı.</summary>
public sealed class EsbEnvelope
{
    [JsonPropertyName("MethodName")] public string MethodName { get; set; } = "";
    [JsonPropertyName("Parameters")] public object? Parameters { get; set; }
    [JsonPropertyName("IsAsync")] public bool IsAsync { get; set; }
    [JsonPropertyName("Application")] public string Application { get; set; } = "PORTAL";
}

/// <summary>/login isteği gövdesi (OSOS portal ile birebir).</summary>
public sealed class LoginRequest
{
    [JsonPropertyName("UserCode")] public string? UserCode { get; set; }
    [JsonPropertyName("Password")] public string? Password { get; set; }
    [JsonPropertyName("LoginType")] public int LoginType { get; set; } = LoginTypes.CustomerPortal;
    [JsonPropertyName("RememberMe")] public bool RememberMe { get; set; }
    [JsonPropertyName("RememberToken")] public string? RememberToken { get; set; }
    /// <summary>Auth-phase token (2FA/AuthCode akışı). Normal ilk girişte null.</summary>
    [JsonPropertyName("Token")] public string? Token { get; set; }
    /// <summary>Token null → TokenGenerate(1); AuthCode akışı → 2.</summary>
    [JsonPropertyName("LoginPhase")] public int LoginPhase { get; set; } = LoginPhases.TokenGenerate;
    /// <summary>Google reCAPTCHA yanıtı. Yalnızca doluysa gönderilir (null → JSON'dan atılır).</summary>
    [JsonPropertyName("GRecaptchaResponse")] public string? GRecaptchaResponse { get; set; }
}

public static class LoginPhases
{
    public const int TokenGenerate = 1;
    public const int AuthCode = 2;
}

/// <summary>/login yanıtı (ilgili alanlar).</summary>
public sealed class LoginResponse
{
    [JsonPropertyName("SessionKey")] public string? SessionKey { get; set; }
    /// <summary>Müşteri hesabının Serno'su (tüm sorgularda kullanılır).</summary>
    [JsonPropertyName("Serno")] public long Serno { get; set; }
    [JsonPropertyName("IdentifierValue")] public string? IdentifierValue { get; set; }
    /// <summary>Kullanıcının tesisat/abone listesi (ham JSON).</summary>
    [JsonPropertyName("Subscriptions")] public JsonElement? Subscriptions { get; set; }
    [JsonPropertyName("IsTwoFactorAutEnable")] public bool? IsTwoFactorAutEnable { get; set; }
    [JsonPropertyName("Message")] public string? Message { get; set; }
}

public static class LoginTypes
{
    // OSOS bundle'ından: CustomerPortal login tipi.
    public const int CustomerPortal = 1;
}

public static class OsosMethods
{
    public const string GetCustomerPortalSubscriptions = "GetCustomerPortalSubscriptions";
    public const string GetCustomerMarks = "GetCustomerMarks";
    public const string GetCustomerSelectedCurrentEndexes = "GetCustomerSelectedCurrentEndexes";
    public const string GetCustomerSelectedConsumptions = "GetCustomerSelectedConsumptions";
    public const string GetCustomerSelectedProfiles = "GetCustomerSelectedProfiles";
    public const string GetDefinitionBySernoParam = "GetDefinitionBySernoParam";
    public const string GetOwnerMontlyEndexConsumptions = "GetOwnerMontlyEndexConsumptions";
    public const string GetOwnerConsumptions = "GetOwnerConsumptions";
}
