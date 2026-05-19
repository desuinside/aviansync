using System.Globalization;
using CsvHelper;

namespace aviansync.Services;

public static class EbLifeListService
{
    public static HashSet<string> LoadScientificNames(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<dynamic>();
        var set = new HashSet<string>();
        foreach (var r in records)
        {
            var dict = r as IDictionary<string, object>;
            if (dict != null && dict.ContainsKey("Scientific Name"))
            {
                var s = dict["Scientific Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s);
            }
        }
        return set;
    }
}
