namespace PetitionService.Server.Models;

public class PetitionCreateDto
{
    public string Content { get; set; } = default!;
    public string? Title { get; set; }
    public string? Summary { get; set; }
}