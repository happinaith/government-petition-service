namespace PetitionService.API.Models;

public class PetitionFilter
{
    public string? Category { get; set; }
    public string? Theme { get; set; }
    public string? Status { get; set; }
    public string? TargetGovernmentLevel { get; set; }
    public string? SearchQuery { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}