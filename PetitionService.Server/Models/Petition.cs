namespace PetitionService.Server.Models;

public class Petition
{
 public int Id { get; set; }
 public string Title { get; set; } = string.Empty;
 public string Content { get; set; } = string.Empty;
 public string? Category { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public string Author { get; set; } = string.Empty;
 public int Signatures { get; set; } =0;
}
