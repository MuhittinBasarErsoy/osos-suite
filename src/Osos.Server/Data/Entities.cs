using Microsoft.AspNetCore.Identity;

namespace Osos.Server.Data;

public sealed class AppUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OsosCredential? OsosCredential { get; set; }
    public List<SearchHistory> Searches { get; set; } = new();
}

/// <summary>Kullanıcının OSOS hesap bilgisi. Şifre DataProtection ile şifreli saklanır.</summary>
public sealed class OsosCredential
{
    public int Id { get; set; }
    public string AppUserId { get; set; } = "";
    public AppUser? AppUser { get; set; }

    public string OsosUserCode { get; set; } = "";
    /// <summary>DataProtection ile korunmuş OSOS şifresi.</summary>
    public string OsosPasswordProtected { get; set; } = "";
    public bool RememberMe { get; set; }
    /// <summary>Login yanıtından gelen müşteri Serno'su (sorgularda varsayılan).</summary>
    public long CustomerSerno { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SearchHistory
{
    public long Id { get; set; }
    public string AppUserId { get; set; } = "";
    public AppUser? AppUser { get; set; }

    public string Screen { get; set; } = "";       // Consumption / Endex / Profiles / Dashboard / Subscriptions
    public string MethodName { get; set; } = "";
    public string ParametersJson { get; set; } = "";
    public long? Serno { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? RowCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SearchResultSnapshot? Snapshot { get; set; }
}

public sealed class SearchResultSnapshot
{
    public long Id { get; set; }
    public long SearchHistoryId { get; set; }
    public SearchHistory? SearchHistory { get; set; }

    public string ResultJson { get; set; } = "";
    public int RowCount { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
