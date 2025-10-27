namespace PetitionService.Server.Models;

public class Petition
{
    public Guid Id { get; set; }
    public string Content { get; set; } = default!;
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public DateTime CreatedAt { get; set; }
}