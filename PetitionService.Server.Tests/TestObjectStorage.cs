using System.Collections.Concurrent;
using PetitionService.Server.Storage;

namespace PetitionService.Server.Tests;

public sealed class TestObjectStorage : IObjectStorage, IDisposable
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(string key, Stream stream, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        _objects[key] = memory.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(key, out var bytes))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new MemoryStream(bytes, writable: false);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(key, out _);
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

    public Task ResetAsync()
    {
        _objects.Clear();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _objects.Clear();
    }
}