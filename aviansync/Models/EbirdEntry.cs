namespace aviansync.Models;

public class EbirdEntry
{
    public string CommonName { get; set; } = "";
    public string Genus { get; set; } = "";
    public string Species { get; set; } = "";
    public string Number { get; set; } = "X";
    public string SpeciesComments { get; set; } = "";
    public string Location { get; set; } = "";
    public string Latitude { get; set; } = "";
    public string Longitude { get; set; } = "";
    public string Date { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string StateProvince { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string Protocol { get; set; } = "Incidental";
    public int NumberOfObservers { get; set; } = 1;
    public int Duration { get; set; } = 0;
    public string AllObservationsReported { get; set; } = "N";
    public int EffortDistanceMiles { get; set; } = 0;
    public int EffortAreaAcres { get; set; } = 0;
    public string SubmissionComments { get; set; } = "";
}
