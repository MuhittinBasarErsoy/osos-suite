namespace Osos.Contracts;

/// <summary>Open-Meteo hava durumu sorgusu.</summary>
public sealed record WeatherQuery
{
    public double Latitude { get; init; } = 40.195;
    public double Longitude { get; init; } = 29.060;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Timezone { get; init; } = "Europe/Istanbul";
    public double? Tilt { get; init; }
    public double? Azimuth { get; init; }
}
