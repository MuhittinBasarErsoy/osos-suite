using System.Text.Json;

namespace Osos.Shared.Services;

public sealed record SubscriptionItem(long Serno, string Label);

/// <summary>/api/osos/me'den gelen abone/tesisat listesini (Serno, Etiket) çiftlerine çevirir.</summary>
public static class SubscriptionParser
{
    public static List<SubscriptionItem> Parse(JsonElement subs)
    {
        var list = new List<SubscriptionItem>();
        if (subs.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in subs.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            list.Add(new SubscriptionItem(ExtractSerno(item), BuildLabel(item)));
        }
        return list;
    }

    private static long ExtractSerno(JsonElement obj)
    {
        foreach (var p in obj.EnumerateObject())
            if (p.Name.Contains("serno", StringComparison.OrdinalIgnoreCase) && TryLong(p.Value, out var s)) return s;
        foreach (var name in new[] { "SubscriptionSerno", "Serno", "Id" })
            if (obj.TryGetProperty(name, out var v) && TryLong(v, out var s2)) return s2;
        return 0;
    }

    private static bool TryLong(JsonElement v, out long val)
    {
        val = 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out val)) return true;
        if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out val)) return true;
        return false;
    }

    private static readonly string[] SkipKeys = { "RecordStatus", "DefinitionType", "AnnounceType" };

    private static string BuildLabel(JsonElement obj)
    {
        string[] prefer = { "Unvan", "Title", "MusteriUnvan", "AboneAdi", "Adres", "Address",
                            "TesisatNo", "Tesisat", "Identifier", "IdentifierValue", "Name", "SayacSeriNo" };
        foreach (var p in prefer)
            if (obj.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                return v.GetString()!.Trim();

        var parts = new List<string>();
        foreach (var p in obj.EnumerateObject())
        {
            if (SkipKeys.Contains(p.Name)) continue;
            if (p.Value.ValueKind == JsonValueKind.String)
            {
                var s = p.Value.GetString();
                if (!string.IsNullOrWhiteSpace(s)) { parts.Add(s!.Trim()); if (parts.Count == 2) break; }
            }
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : "Tesisat";
    }
}
