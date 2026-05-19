using CsvHelper;
using System.Globalization;
using System.Text;
using aviansync.Models;

namespace aviansync.Services;

public static class CsvService
{
    public static string WriteEntries(IEnumerable<EbirdEntry> entries, string outDir, string userId)
    {
        Directory.CreateDirectory(outDir);
        var file = Path.Combine(outDir, $"all_bird_entries_{userId}.csv");
        using var writer = new StreamWriter(file, false, Encoding.UTF8);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var e in entries)
        {
            csv.WriteRecord(e);
            csv.NextRecord();
        }
        return file;
    }
}
