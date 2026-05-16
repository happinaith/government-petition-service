using System.Security.Cryptography;

namespace PetitionService.Server.Storage;

public class LocalObjectStorage : IObjectStorage
{
 private readonly string _rootPath;

 public LocalObjectStorage(IConfiguration config)
 {
 _rootPath = config["ObjectStorage:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "object-storage");
 Directory.CreateDirectory(_rootPath);
 }

 public async Task SaveAsync(string key, Stream stream, CancellationToken cancellationToken = default)
 {
 var fullPath = BuildPath(key);
 var directory = Path.GetDirectoryName(fullPath);
 if (!string.IsNullOrWhiteSpace(directory))
 {
 Directory.CreateDirectory(directory);
 }

 await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
 await stream.CopyToAsync(fs, cancellationToken);
 }

 public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
 {
 var fullPath = BuildPath(key);
 if (!File.Exists(fullPath))
 {
 return Task.FromResult<Stream?>(null);
 }

 Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
 return Task.FromResult<Stream?>(stream);
 }

 public Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken = default)
 {
 var fullPath = BuildPath(key);
 if (File.Exists(fullPath))
 {
 File.Delete(fullPath);
 }

 return Task.CompletedTask;
 }

 public Task<string?> CreatePreSignedDownloadUrlAsync(
 string key,
 string fileName,
 TimeSpan ttl,
 bool asAttachment = true,
 CancellationToken cancellationToken = default)
 {
 return Task.FromResult<string?>(null);
 }

 private string BuildPath(string key)
 {
 var sanitized = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
 var fullPath = Path.GetFullPath(Path.Combine(_rootPath, sanitized));
 var fullRoot = Path.GetFullPath(_rootPath);
 if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
 {
 throw new CryptographicException("Invalid object storage key.");
 }

 return fullPath;
 }
}