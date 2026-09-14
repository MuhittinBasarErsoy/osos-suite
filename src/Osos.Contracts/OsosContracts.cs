using System.Text.Json;

namespace Osos.Contracts;

/// <summary>Tüm sorgu ekranlarının ortak alanları.</summary>
public abstract record OsosQueryBase
{
    public long Serno { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    /// <summary>Seçili tesisat Serno'ları; boş = tümü.</summary>
    public long[]? Selected { get; init; }
    public int TotalItemCount { get; init; }
}

/// <summary>Giriş yapan müşterinin Serno'su + tesisat/abone listesi.</summary>
public sealed record MeDto(long Serno, JsonElement Subscriptions);

public sealed record ConsumptionQuery : OsosQueryBase
{
    /// <summary>OSOS "Type" (ör. 2). Portaldaki Kümülatif/period seçimi.</summary>
    public int Type { get; init; } = 2;
    public int Period { get; init; } = 0;
}

public sealed record EndexQuery : OsosQueryBase;

public sealed record ProfilesQuery : OsosQueryBase
{
    public bool WithoutMultiplier { get; init; } = true;
}

public sealed record SubscriptionsQuery
{
    public long Serno { get; init; }
    public int PageSize { get; init; } = 1000;
    public int PageNumber { get; init; } = 1;
}

public sealed record OwnerConsumptionsQuery
{
    public long OwnerSerno { get; init; }
    public int OwnerType { get; init; } = 15;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public bool IsOnlySuccess { get; init; } = true;
    public bool IncludeLoadProfiles { get; init; }
    public bool WithoutMultiplier { get; init; }
    public bool MergeResult { get; init; } = true;
}

/// <summary>OSOS'tan dönen ham sonuç + kaydedilen geçmiş kimliği.</summary>
public sealed record OsosResult(string RawJson, int RowCount, long SearchHistoryId);

// ---- Arama geçmişi ----

public sealed record SearchHistoryDto(
    long Id,
    string Screen,
    string MethodName,
    string ParametersJson,
    long? Serno,
    DateTime? StartDate,
    DateTime? EndDate,
    int? RowCount,
    DateTime CreatedAt);

public sealed record SearchResultDto(long SearchHistoryId, string ResultJson, int RowCount, DateTime CapturedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
