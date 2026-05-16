using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetitionService.Server.Data;
using PetitionService.Server.Models;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using PetitionService.Server.Storage;
using PetitionService.Server.AI;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;
using System.Text;

namespace PetitionService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PetitionsController : ControllerBase
{
 private static readonly string[] AllowedSortFields = ["createdAt", "title", "author", "signatures"];
 private static readonly string[] AllowedSortDirections = ["asc", "desc"];

 private readonly AppDbContext _db;
 private readonly IObjectStorage _objectStorage;
 private readonly string _tokenSigningKey;
 private readonly long _maxFileSizeBytes;
 private readonly string[] _allowedContentTypes;
 private readonly int _preSignedUrlTtlMinutes;
 private readonly IGeminiPetitionAssistant _geminiPetitionAssistant;

 public PetitionsController(
 AppDbContext db,
 IObjectStorage objectStorage,
 IConfiguration config,
 IGeminiPetitionAssistant geminiPetitionAssistant)
 {
 _db = db;
 _objectStorage = objectStorage;
 _geminiPetitionAssistant = geminiPetitionAssistant;
 _tokenSigningKey = config["Jwt:Key"] ?? "DEV_KEY_CHANGE_ME_123456789_ABCDEF_123456789";
 _maxFileSizeBytes = config.GetValue<long?>("ObjectStorage:MaxFileSizeBytes") ?? 5 * 1024 * 1024;
 _allowedContentTypes = config.GetSection("ObjectStorage:AllowedContentTypes").Get<string[]>()
 ?? ["application/pdf", "image/png", "image/jpeg", "text/plain"];
 _preSignedUrlTtlMinutes = config.GetValue<int?>("ObjectStorage:PreSignedUrlTtlMinutes") ?? 5;
 }

 public class PetitionListQuery
 {
 [StringLength(128)]
 public string? Category { get; init; }

 [StringLength(128)]
 public string? Author { get; init; }

 [StringLength(256)]
 public string? Q { get; init; }

 [Range(0, int.MaxValue)]
 public int? MinSignatures { get; init; }

 [Range(0, int.MaxValue)]
 public int? MaxSignatures { get; init; }

 [StringLength(32)]
 public string? SortBy { get; init; }

 [StringLength(8)]
 public string? SortDir { get; init; }

 [Range(1, 100000)]
 public int Page { get; init; } = 1;

 [Range(1, 100)]
 public int PageSize { get; init; } = 10;
 }

 public record PetitionListResponse(
 IEnumerable<PetitionListItemResponse> Items,
 int TotalCount,
 int Page,
 int PageSize,
 string SortBy,
 string SortDir);

 public record PetitionListItemResponse(
 int Id,
 string Title,
 string? Category,
 DateTime CreatedAt,
 string Author,
 int Signatures);

 public record PetitionAttachmentResponse(
 int Id,
 int PetitionId,
 string FileName,
 string ContentType,
 long SizeBytes,
 string UploadedBy,
 DateTime UploadedAt);

 public record PreSignedDownloadResponse(string Url, DateTime ExpiresAtUtc);

 public class PetitionCreateRequest
 {
 [Required]
 [StringLength(200, MinimumLength = 3)]
 public string Title { get; init; } = string.Empty;

 [Required]
 [StringLength(5000, MinimumLength = 10)]
 public string Content { get; init; } = string.Empty;

 [StringLength(100)]
 public string? Category { get; init; }
 }

 public class PetitionUpdateRequest
 {
 [Required]
 [StringLength(200, MinimumLength = 3)]
 public string Title { get; init; } = string.Empty;

 [Required]
 [StringLength(5000, MinimumLength = 10)]
 public string Content { get; init; } = string.Empty;

 [StringLength(100)]
 public string? Category { get; init; }
 }

 public class PetitionAiAssistRequest
 {
 [Required]
 [StringLength(5000, MinimumLength = 10)]
 public string Content { get; init; } = string.Empty;

 [StringLength(200)]
 public string? TitleHint { get; init; }

 [StringLength(100)]
 public string? CategoryHint { get; init; }
 }

 public record PetitionAiAssistResponse(
 string Title,
 string Content,
 string? Category,
 string Summary,
 string Provider,
 string Model);

 [HttpGet]
 public async Task<ActionResult<PetitionListResponse>> GetAll([FromQuery] PetitionListQuery request)
 {
 var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "createdAt" : request.SortBy.Trim();
 var sortDir = string.IsNullOrWhiteSpace(request.SortDir) ? "desc" : request.SortDir.Trim().ToLowerInvariant();

 if (!AllowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
 {
 ModelState.AddModelError(nameof(request.SortBy), "sortBy must be one of: createdAt, title, author, signatures.");
 }

 if (!AllowedSortDirections.Contains(sortDir, StringComparer.OrdinalIgnoreCase))
 {
 ModelState.AddModelError(nameof(request.SortDir), "sortDir must be either asc or desc.");
 }

 if (request.MinSignatures.HasValue && request.MaxSignatures.HasValue && request.MinSignatures.Value > request.MaxSignatures.Value)
 {
 ModelState.AddModelError(nameof(request.MinSignatures), "minSignatures cannot be greater than maxSignatures.");
 }

 if (!ModelState.IsValid)
 {
 return ValidationProblem(ModelState);
 }

 var query = _db.Petitions.AsNoTracking().AsQueryable();

 if (!string.IsNullOrWhiteSpace(request.Category))
 query = query.Where(p => p.Category == request.Category);

 if (!string.IsNullOrWhiteSpace(request.Author))
 query = query.Where(p => p.Author.Contains(request.Author));

 if (!string.IsNullOrWhiteSpace(request.Q))
 {
 query = query.Where(p =>
 p.Title.Contains(request.Q) ||
 p.Content.Contains(request.Q) ||
 p.Author.Contains(request.Q) ||
 (p.Category != null && p.Category.Contains(request.Q)));
 }

 if (request.MinSignatures.HasValue)
 query = query.Where(p => p.Signatures >= request.MinSignatures.Value);

 if (request.MaxSignatures.HasValue)
 query = query.Where(p => p.Signatures <= request.MaxSignatures.Value);

 query = (sortBy.ToLowerInvariant(), sortDir) switch
 {
 ("title", "asc") => query.OrderBy(p => p.Title),
 ("title", "desc") => query.OrderByDescending(p => p.Title),
 ("author", "asc") => query.OrderBy(p => p.Author),
 ("author", "desc") => query.OrderByDescending(p => p.Author),
 ("signatures", "asc") => query.OrderBy(p => p.Signatures),
 ("signatures", "desc") => query.OrderByDescending(p => p.Signatures),
 ("createdat", "asc") => query.OrderBy(p => p.CreatedAt),
 _ => query.OrderByDescending(p => p.CreatedAt)
 };

 var totalCount = await query.CountAsync();
 var items = await query
 .Skip((request.Page -1) * request.PageSize)
 .Take(request.PageSize)
 .Select(p => new PetitionListItemResponse(
 p.Id,
 p.Title,
 p.Category,
 p.CreatedAt,
 p.Author,
 p.Signatures))
 .ToListAsync();

 return Ok(new PetitionListResponse(items, totalCount, request.Page, request.PageSize, sortBy, sortDir));
 }

 [HttpGet("{id:int}")]
 public async Task<ActionResult<Petition>> Get(int id)
 {
 var entity = await _db.Petitions.FindAsync(id);
 return entity is null ? NotFound() : Ok(entity);
 }

 [HttpGet("{id:int}/attachments")]
 [Authorize]
 public async Task<ActionResult<IEnumerable<PetitionAttachmentResponse>>> GetAttachments(int id)
 {
 var petition = await _db.Petitions.FindAsync(id);
 if (petition is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Petition not found",
 Detail = "The petition does not exist.",
 Status = StatusCodes.Status404NotFound
 });
 }

 if (!CanManagePetition(petition))
 {
 return Forbid();
 }

 var items = await _db.PetitionAttachments
 .Where(a => a.PetitionId == id)
 .OrderByDescending(a => a.UploadedAt)
 .Select(a => new PetitionAttachmentResponse(
 a.Id,
 a.PetitionId,
 a.OriginalFileName,
 a.ContentType,
 a.SizeBytes,
 a.UploadedBy,
 a.UploadedAt))
 .ToListAsync();

 return Ok(items);
 }

 [HttpPost("{id:int}/attachments")]
 [Authorize(Roles = "User,Admin")]
 [Consumes("multipart/form-data")]
 [RequestSizeLimit(10_485_760)]
 public async Task<ActionResult<PetitionAttachmentResponse>> UploadAttachment(
 int id,
 IFormFile? file,
 CancellationToken cancellationToken)
 {
 var petition = await _db.Petitions.FindAsync([id], cancellationToken);
 if (petition is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Petition not found",
 Detail = "The petition does not exist.",
 Status = StatusCodes.Status404NotFound
 });
 }

 if (!CanManagePetition(petition))
 {
 return Forbid();
 }

 if (file is null || file.Length == 0)
 {
 return BadRequest(new ProblemDetails
 {
 Title = "Invalid file",
 Detail = "File is required and must not be empty.",
 Status = StatusCodes.Status400BadRequest
 });
 }

 if (file.Length > _maxFileSizeBytes)
 {
 return BadRequest(new ProblemDetails
 {
 Title = "File too large",
 Detail = $"Maximum allowed size is {_maxFileSizeBytes} bytes.",
 Status = StatusCodes.Status400BadRequest
 });
 }

 if (!_allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
 {
 return BadRequest(new ProblemDetails
 {
 Title = "Unsupported file type",
 Detail = $"Allowed types: {string.Join(", ", _allowedContentTypes)}.",
 Status = StatusCodes.Status400BadRequest
 });
 }

 var safeFileName = Path.GetFileName(file.FileName);
 if (string.IsNullOrWhiteSpace(safeFileName))
 {
 return BadRequest(new ProblemDetails
 {
 Title = "Invalid file name",
 Detail = "File name is not valid.",
 Status = StatusCodes.Status400BadRequest
 });
 }

 var duplicateName = await _db.PetitionAttachments.AnyAsync(
 a => a.PetitionId == id && a.OriginalFileName == safeFileName,
 cancellationToken);

 if (duplicateName)
 {
 return Conflict(new ProblemDetails
 {
 Title = "Attachment conflict",
 Detail = "An attachment with the same file name already exists for this petition.",
 Status = StatusCodes.Status409Conflict
 });
 }

 var storageKey = $"petitions/{id}/{Guid.NewGuid():N}_{safeFileName}";
 await using var readStream = file.OpenReadStream();
 await _objectStorage.SaveAsync(storageKey, readStream, cancellationToken);

 var uploadedBy = User.Identity?.Name ?? "unknown";
 var attachment = new PetitionAttachment
 {
 PetitionId = id,
 StorageKey = storageKey,
 OriginalFileName = safeFileName,
 ContentType = file.ContentType,
 SizeBytes = file.Length,
 UploadedBy = uploadedBy,
 UploadedAt = DateTime.UtcNow
 };

 _db.PetitionAttachments.Add(attachment);
 await _db.SaveChangesAsync(cancellationToken);

 return CreatedAtAction(
 nameof(GetAttachments),
 new { id },
 new PetitionAttachmentResponse(
 attachment.Id,
 attachment.PetitionId,
 attachment.OriginalFileName,
 attachment.ContentType,
 attachment.SizeBytes,
 attachment.UploadedBy,
 attachment.UploadedAt));
 }

 [HttpPost("{petitionId:int}/attachments/{attachmentId:int}/presigned-download")]
 [Authorize]
 public async Task<ActionResult<PreSignedDownloadResponse>> CreatePreSignedDownloadUrl(
 int petitionId,
 int attachmentId,
 [FromQuery] bool inline = false)
 {
 var petition = await _db.Petitions.FindAsync(petitionId);
 if (petition is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Petition not found",
 Detail = "The petition does not exist.",
 Status = StatusCodes.Status404NotFound
 });
 }

 if (!CanManagePetition(petition))
 {
 return Forbid();
 }

 var attachment = await _db.PetitionAttachments
 .FirstOrDefaultAsync(a => a.Id == attachmentId && a.PetitionId == petitionId);
 if (attachment is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Attachment not found",
 Detail = "The attachment does not exist for this petition.",
 Status = StatusCodes.Status404NotFound
 });
 }

 var expiresAt = DateTime.UtcNow.AddMinutes(_preSignedUrlTtlMinutes);
 if (!inline)
 {
 var providerUrl = await _objectStorage.CreatePreSignedDownloadUrlAsync(
 attachment.StorageKey,
 attachment.OriginalFileName,
 TimeSpan.FromMinutes(_preSignedUrlTtlMinutes),
 asAttachment: true);

 if (!string.IsNullOrWhiteSpace(providerUrl))
 {
 return Ok(new PreSignedDownloadResponse(providerUrl, expiresAt));
 }
 }

 var token = CreatePreSignedToken(attachment.Id, expiresAt);
 var url = inline
 ? $"/api/petitions/attachments/preview?token={Uri.EscapeDataString(token)}"
 : $"/api/petitions/attachments/download?token={Uri.EscapeDataString(token)}";
 return Ok(new PreSignedDownloadResponse(url, expiresAt));
 }

 [HttpGet("attachments/download")]
 [AllowAnonymous]
 public async Task<ActionResult> DownloadAttachment([FromQuery] string token, CancellationToken cancellationToken)
 {
 if (string.IsNullOrWhiteSpace(token))
 {
 return Unauthorized();
 }

 if (!TryValidatePreSignedToken(token, out var attachmentId))
 {
 return Unauthorized();
 }

 var attachment = await _db.PetitionAttachments.FindAsync([attachmentId], cancellationToken);
 if (attachment is null)
 {
 return NotFound();
 }

 var stream = await _objectStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
 if (stream is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Object not found",
 Detail = "Attachment data is missing in object storage.",
 Status = StatusCodes.Status404NotFound
 });
 }

 return File(stream, attachment.ContentType, attachment.OriginalFileName, enableRangeProcessing: true);
 }

 [HttpGet("attachments/preview")]
 [AllowAnonymous]
 public async Task<ActionResult> PreviewAttachment([FromQuery] string token, CancellationToken cancellationToken)
 {
 if (string.IsNullOrWhiteSpace(token))
 {
 return Unauthorized();
 }

 if (!TryValidatePreSignedToken(token, out var attachmentId))
 {
 return Unauthorized();
 }

 var attachment = await _db.PetitionAttachments.FindAsync([attachmentId], cancellationToken);
 if (attachment is null)
 {
 return NotFound();
 }

 var stream = await _objectStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
 if (stream is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Object not found",
 Detail = "Attachment data is missing in object storage.",
 Status = StatusCodes.Status404NotFound
 });
 }

 return File(stream, attachment.ContentType, enableRangeProcessing: true);
 }

 [HttpDelete("{petitionId:int}/attachments/{attachmentId:int}")]
 [Authorize(Roles = "User,Admin")]
 public async Task<ActionResult> DeleteAttachment(int petitionId, int attachmentId, CancellationToken cancellationToken)
 {
 var petition = await _db.Petitions.FindAsync([petitionId], cancellationToken);
 if (petition is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Petition not found",
 Detail = "The petition does not exist.",
 Status = StatusCodes.Status404NotFound
 });
 }

 if (!CanManagePetition(petition))
 {
 return Forbid();
 }

 var attachment = await _db.PetitionAttachments
 .FirstOrDefaultAsync(a => a.Id == attachmentId && a.PetitionId == petitionId, cancellationToken);
 if (attachment is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Attachment not found",
 Detail = "The attachment does not exist for this petition.",
 Status = StatusCodes.Status404NotFound
 });
 }

 await _objectStorage.DeleteIfExistsAsync(attachment.StorageKey, cancellationToken);
 _db.PetitionAttachments.Remove(attachment);
 await _db.SaveChangesAsync(cancellationToken);
 return NoContent();
 }

 [HttpPost]
 [Authorize(Roles = "User,Admin")]
 public async Task<ActionResult<Petition>> Create([FromBody] PetitionCreateRequest request)
 {
 var currentUser = User.Identity?.Name;
 if (string.IsNullOrWhiteSpace(currentUser))
 {
 return Unauthorized();
 }

 var normalizedTitle = request.Title.Trim().ToLowerInvariant();
 var duplicate = await _db.Petitions.AnyAsync(p =>
 p.Author == currentUser &&
 p.Title.ToLower() == normalizedTitle);

 if (duplicate)
 {
 return Conflict(new ProblemDetails
 {
 Title = "Petition conflict",
 Detail = "A petition with the same title already exists for the current author.",
 Status = StatusCodes.Status409Conflict
 });
 }

 var petition = new Petition
 {
 Id =0,
 Title = request.Title.Trim(),
 Content = request.Content.Trim(),
 Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
 CreatedAt = DateTime.UtcNow,
 Author = currentUser,
 Signatures = 0
 };

 _db.Petitions.Add(petition);
 await _db.SaveChangesAsync();
 return CreatedAtAction(nameof(Get), new { id = petition.Id }, petition);
 }

 [HttpPost("ai-draft")]
 [Authorize(Roles = "User,Admin")]
 [EnableRateLimiting("gemini-assist")]
 public async Task<ActionResult<PetitionAiAssistResponse>> BuildAiDraft(
 [FromBody] PetitionAiAssistRequest request,
 CancellationToken cancellationToken)
 {
 try
 {
 var suggestion = await _geminiPetitionAssistant.BuildDraftAsync(
 new PetitionAiDraftRequest(request.Content, request.TitleHint, request.CategoryHint),
 cancellationToken);

 return Ok(new PetitionAiAssistResponse(
 suggestion.Title,
 suggestion.Content,
 suggestion.Category,
 suggestion.Summary,
 suggestion.Provider,
 suggestion.Model));
 }
 catch (GeminiIntegrationException ex)
 {
 return Problem(
 title: "Gemini integration error",
 detail: ex.Message,
 statusCode: ex.StatusCode);
 }
 }

 [HttpPut("{id:int}")]
 [Authorize(Roles = "User,Admin")]
 public async Task<ActionResult> Update(int id, [FromBody] PetitionUpdateRequest request)
 {
 var entity = await _db.Petitions.FindAsync(id);
 if (entity is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Petition not found",
 Detail = "The petition does not exist.",
 Status = StatusCodes.Status404NotFound
 });
 }

 var currentUser = User.Identity?.Name;
 var isAdmin = User.IsInRole("Admin");
 var isAuthor = !string.IsNullOrWhiteSpace(currentUser) && entity.Author == currentUser;
 if (!isAdmin && !isAuthor)
 {
 return Forbid();
 }

 var normalizedTitle = request.Title.Trim().ToLowerInvariant();
 var duplicate = await _db.Petitions.AnyAsync(p =>
 p.Id != id &&
 p.Author == entity.Author &&
 p.Title.ToLower() == normalizedTitle);

 if (duplicate)
 {
 return Conflict(new ProblemDetails
 {
 Title = "Petition conflict",
 Detail = "A petition with the same title already exists for this author.",
 Status = StatusCodes.Status409Conflict
 });
 }

 entity.Title = request.Title.Trim();
 entity.Content = request.Content.Trim();
 entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();

 await _db.SaveChangesAsync();
 return NoContent();
 }

 [HttpDelete("{id:int}")]
 [Authorize(Roles = "User,Admin")]
 public async Task<ActionResult> Delete(int id)
 {
 var entity = await _db.Petitions.FindAsync(id);
 if (entity is null)
 {
 return NotFound(new ProblemDetails
 {
 Title = "Petition not found",
 Detail = "The petition does not exist.",
 Status = StatusCodes.Status404NotFound
 });
 }

 var currentUser = User.Identity?.Name;
 var isAdmin = User.IsInRole("Admin");
 var isAuthor = !string.IsNullOrWhiteSpace(currentUser) && entity.Author == currentUser;
 if (!isAdmin && !isAuthor)
 {
 return Forbid();
 }

 var attachments = await _db.PetitionAttachments.Where(a => a.PetitionId == id).ToListAsync();
 foreach (var attachment in attachments)
 {
 await _objectStorage.DeleteIfExistsAsync(attachment.StorageKey);
 }

 _db.PetitionAttachments.RemoveRange(attachments);

 _db.Petitions.Remove(entity);
 await _db.SaveChangesAsync();
 return NoContent();
 }

 [HttpPost("{id:int}/sign")] 
 public async Task<ActionResult<Petition>> Sign(int id)
 {
 var entity = await _db.Petitions.FindAsync(id);
 if (entity is null) return NotFound();
 entity.Signatures +=1;
 await _db.SaveChangesAsync();
 return Ok(entity);
 }

 private bool CanManagePetition(Petition petition)
 {
 var currentUser = User.Identity?.Name;
 var isAdmin = User.IsInRole("Admin");
 var isAuthor = !string.IsNullOrWhiteSpace(currentUser) && petition.Author == currentUser;
 return isAdmin || isAuthor;
 }

 private string CreatePreSignedToken(int attachmentId, DateTime expiresAtUtc)
 {
 var expiresUnix = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds();
 var payload = $"{attachmentId}:{expiresUnix}";
 var payloadBytes = Encoding.UTF8.GetBytes(payload);

 using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_tokenSigningKey));
 var signature = hmac.ComputeHash(payloadBytes);

 return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
 }

 private bool TryValidatePreSignedToken(string token, out int attachmentId)
 {
 attachmentId = 0;
 var parts = token.Split('.');
 if (parts.Length != 2)
 {
 return false;
 }

 byte[] payloadBytes;
 byte[] signatureBytes;
 try
 {
 payloadBytes = Base64UrlDecode(parts[0]);
 signatureBytes = Base64UrlDecode(parts[1]);
 }
 catch
 {
 return false;
 }

 using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_tokenSigningKey));
 var computed = hmac.ComputeHash(payloadBytes);
 if (!CryptographicOperations.FixedTimeEquals(computed, signatureBytes))
 {
 return false;
 }

 var payload = Encoding.UTF8.GetString(payloadBytes);
 var payloadParts = payload.Split(':');
 if (payloadParts.Length != 2)
 {
 return false;
 }

 if (!int.TryParse(payloadParts[0], out var parsedAttachmentId))
 {
 return false;
 }

 if (!long.TryParse(payloadParts[1], out var expiresUnix))
 {
 return false;
 }

 var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
 if (expiresUnix < nowUnix)
 {
 return false;
 }

 attachmentId = parsedAttachmentId;
 return true;
 }

 private static string Base64UrlEncode(byte[] input)
 {
 return Convert.ToBase64String(input)
 .TrimEnd('=')
 .Replace('+', '-')
 .Replace('/', '_');
 }

 private static byte[] Base64UrlDecode(string input)
 {
 var normalized = input.Replace('-', '+').Replace('_', '/');
 var padding = 4 - normalized.Length % 4;
 if (padding is > 0 and < 4)
 {
 normalized = normalized.PadRight(normalized.Length + padding, '=');
 }

 return Convert.FromBase64String(normalized);
 }
}
