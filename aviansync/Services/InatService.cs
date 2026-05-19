using System.Net.Http;
using System.Text.Json;

namespace aviansync.Services;

public class InatService
{
    private readonly HttpClient _http;
    public InatService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<JsonElement>> FetchAllObservations(string userId)
    {
        var baseUrl = $"https://api.inaturalist.org/v1/observations?taxon_id=3&user_id={userId}&quality_grade=research&per_page=200&order=desc&order_by=observed_on";
        var all = new List<JsonElement>();
        int page = 1;
        while (true)
        {
            var url = baseUrl + "&page=" + page;
            var res = await _http.GetAsync(url);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results)) break;
            if (results.GetArrayLength() == 0) break;
            // Clone each element so it lives independently after the JsonDocument is disposed
            foreach (var r in results.EnumerateArray()) all.Add(r.Clone());
            page++;
        }
        return all;
    }
}
