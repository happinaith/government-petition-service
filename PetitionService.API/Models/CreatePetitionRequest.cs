namespace PetitionService.API.Models;

public class CreatePetitionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string TargetGovernmentLevel { get; set; } = string.Empty;
}