namespace PetitionService.Server.Storage;

public interface IObjectStorage
{
 Task SaveAsync(string key, Stream stream, CancellationToken cancellationToken = default);
 Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
 Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken = default);
 Task<string?> CreatePreSignedDownloadUrlAsync(
 string key,
 string fileName,
 TimeSpan ttl,
 bool asAttachment = true,
 CancellationToken cancellationToken = default);
}