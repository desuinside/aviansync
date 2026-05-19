using System.Text.Json.Serialization;

namespace aviansync.Models;

public class RowData
{
    [JsonPropertyName("english")]    public string English    { get; set; } = "";
    [JsonPropertyName("latin")]      public string Latin      { get; set; } = "";
    [JsonPropertyName("date")]       public string Date       { get; set; } = "";
    [JsonPropertyName("inat")]       public bool   Inat       { get; set; } = true;
    [JsonPropertyName("ebird")]      public bool   Ebird      { get; set; } = false;
    [JsonPropertyName("taxonValid")] public bool   TaxonValid { get; set; } = true;
}
