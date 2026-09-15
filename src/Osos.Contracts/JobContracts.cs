namespace Osos.Contracts;

/// <summary>Anlık (tek seferlik) iş tetikleme.</summary>
public sealed record RunNowRequest(string Screen, long Serno, int DaysBack, int Type);

/// <summary>Zamanlanmış (cron) iş oluşturma/güncelleme.</summary>
public sealed record ScheduleJobRequest(string Screen, long Serno, int DaysBack, int Type, string Cron, string? Name);

/// <summary>Zamanlanmış iş özeti (liste için).</summary>
public sealed record JobDto(
    string Id,
    string Screen,
    long Serno,
    int DaysBack,
    string Cron,
    string? NextRun,
    string? LastRun,
    string? LastState);
