namespace PetitionService.Server.Models;

public class UserSecurityState
{
    public string UserId { get; set; } = string.Empty;
    public int TokenVersion { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}