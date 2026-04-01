namespace PetitionService.Server.Models;

public class PetitionSignature
{
    public int Id { get; set; }
    public int PetitionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
}