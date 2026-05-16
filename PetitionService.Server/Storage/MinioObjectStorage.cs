using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

namespace PetitionService.Server.Storage;

public class MinioObjectStorage : IObjectStorage
{
 private readonly IAmazonS3 _client;
 private readonly string _bucket;
 private readonly Uri _endpointUri;
 private readonly SemaphoreSlim _bucketInitLock = new(1, 1);
 private bool _bucketReady;

 public MinioObjectStorage(IConfiguration config)
 {
 var endpoint = config["ObjectStorage:Minio:Endpoint"] ?? "http://localhost:9000";
 _endpointUri = new Uri(endpoint);
 var accessKey = config["ObjectStorage:Minio:AccessKey"] ?? "minioadmin";
 var secretKey = config["ObjectStorage:Minio:SecretKey"] ?? "minioadmin";
 _bucket = config["ObjectStorage:Minio:Bucket"] ?? "petition-attachments";
 var forcePathStyle = config.GetValue<bool?>("ObjectStorage:Minio:ForcePathStyle") ?? true;

 var credentials = new BasicAWSCredentials(accessKey, secretKey);
 var s3Config = new AmazonS3Config
 {
 ServiceURL = endpoint,
 UseHttp = string.Equals(_endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase),
 ForcePathStyle = forcePathStyle,
 SignatureVersion = "4"
 };

 _client = new AmazonS3Client(credentials, s3Config);
 }

 public async Task SaveAsync(string key, Stream stream, CancellationToken cancellationToken = default)
 {
 await EnsureBucketAsync(cancellationToken);

 var request = new PutObjectRequest
 {
 BucketName = _bucket,
 Key = key,
 InputStream = stream,
 AutoCloseStream = false
 };

 await _client.PutObjectAsync(request, cancellationToken);
 }

 public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
 {
 await EnsureBucketAsync(cancellationToken);

 try
 {
 using var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);
 var memory = new MemoryStream();
 await response.ResponseStream.CopyToAsync(memory, cancellationToken);
 memory.Position = 0;
 return memory;
 }
 catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
 {
 return null;
 }
 }

 public async Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken = default)
 {
 await EnsureBucketAsync(cancellationToken);

 try
 {
 await _client.DeleteObjectAsync(_bucket, key, cancellationToken);
 }
 catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
 {
 // already deleted
 }
 }

 public async Task<string?> CreatePreSignedDownloadUrlAsync(
 string key,
 string fileName,
 TimeSpan ttl,
 bool asAttachment = true,
 CancellationToken cancellationToken = default)
 {
 await EnsureBucketAsync(cancellationToken);

 var request = new GetPreSignedUrlRequest
 {
 BucketName = _bucket,
 Key = key,
 Expires = DateTime.UtcNow.Add(ttl),
 ResponseHeaderOverrides = new ResponseHeaderOverrides
 {
 ContentDisposition = asAttachment
 ? $"attachment; filename=\"{fileName}\""
 : $"inline; filename=\"{fileName}\""
 }
 };

 var rawUrl = _client.GetPreSignedURL(request);
 var rawUri = new Uri(rawUrl);

 var normalized = new UriBuilder(rawUri)
 {
 Scheme = _endpointUri.Scheme,
 Host = _endpointUri.Host,
 Port = _endpointUri.IsDefaultPort ? -1 : _endpointUri.Port
 };

 return normalized.Uri.ToString();
 }

 private async Task EnsureBucketAsync(CancellationToken cancellationToken)
 {
 if (_bucketReady)
 {
 return;
 }

 await _bucketInitLock.WaitAsync(cancellationToken);
 try
 {
 if (_bucketReady)
 {
 return;
 }

 if (!await AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucket))
 {
 await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, cancellationToken);
 }

 _bucketReady = true;
 }
 finally
 {
 _bucketInitLock.Release();
 }
 }
}