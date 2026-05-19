using System.Text.Json;

namespace aviansync.Services;

public class EbirdTaxonomyService(HttpClient http)
{
    private static Dictionary<string, string>? _cache; // binomial → eBird comName
    private static DateTime _cacheTime = DateTime.MinValue;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    // Returns null when no API key provided (validation skipped).
    // Returns a map of binomial scientific name → eBird official common name.
    public async Task<Dictionary<string, string>?> GetTaxonomyAsync(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        await _lock.WaitAsync();
        try
        {
            if (_cache != null && DateTime.UtcNow - _cacheTime < TimeSpan.FromHours(24))
                return _cache;

            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://api.ebird.org/v2/ref/taxonomy/ebird?fmt=json&cat=species");
            req.Headers.Add("X-eBirdApiToken", apiKey);

            var resp = await http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var sciName = e.TryGetProperty("sciName", out var s) ? s.GetString() ?? "" : "";
                var comName = e.TryGetProperty("comName", out var c) ? c.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(sciName))
                    _cache[sciName] = comName;
            }
            _cacheTime = DateTime.UtcNow;
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }
}
