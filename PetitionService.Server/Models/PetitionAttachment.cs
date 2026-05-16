namespace PetitionService.Server.Models;

public class PetitionAttachment
{
 public int Id { get; set; }
 public int PetitionId { get; set; }
 public string StorageKey { get; set; } = string.Empty;
 public string OriginalFileName { get; set; } = string.Empty;
 public string ContentType { get; set; } = string.Empty;
 public long SizeBytes { get; set; }
 public string UploadedBy { get; set; } = string.Empty;
 public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}