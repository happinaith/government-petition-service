namespace PetitionService.API.Models;

public class Petition
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime LastUpdated { get; set; }
    public int SignatureCount { get; set; }
    public string Status { get; set; } = "Active"; // Active, Closed, Under Review
    public string CreatedBy { get; set; } = string.Empty;
    public string TargetGovernmentLevel { get; set; } = string.Empty; // Federal, State, Local
}