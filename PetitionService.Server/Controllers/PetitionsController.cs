using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using PetitionService.Server.Models;

namespace PetitionService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PetitionsController : ControllerBase
{
    // Простейшее in-memory-хранилище для быстрого старта.
    private static readonly ConcurrentDictionary<Guid, Petition> _store = new();

    private readonly ILogger<PetitionsController> _logger;

    public PetitionsController(ILogger<PetitionsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public Task<IEnumerable<Petition>> GetAll(CancellationToken ct)
    {
        var list = _store.Values.OrderByDescending(p => p.CreatedAt);
        return Task.FromResult<IEnumerable<Petition>>(list);
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_store.TryGetValue(id, out var petition))
            return Task.FromResult<IActionResult>(NotFound());

        return Task.FromResult<IActionResult>(Ok(petition));
    }

    [HttpPost]
    public Task<IActionResult> Create([FromBody] PetitionCreateDto dto, CancellationToken ct)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Content))
            return Task.FromResult<IActionResult>(BadRequest("Content is required."));

        var petition = new Petition
        {
            Id = Guid.NewGuid(),
            Content = dto.Content,
            Title = dto.Title,
            Summary = dto.Summary,
            CreatedAt = DateTime.UtcNow
        };

        _store[petition.Id] = petition;

        return Task.FromResult<IActionResult>(CreatedAtAction(nameof(GetById), new { id = petition.Id }, petition));
    }

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] PetitionUpdateDto dto, CancellationToken ct)
    {
        if (!_store.TryGetValue(id, out var existing))
            return Task.FromResult<IActionResult>(NotFound());

        // Частичное обновление
        if (!string.IsNullOrWhiteSpace(dto.Content))
            existing.Content = dto.Content;
        if (dto.Title is not null)
            existing.Title = dto.Title;
        if (dto.Summary is not null)
            existing.Summary = dto.Summary;

        _store[id] = existing;
        return Task.FromResult<IActionResult>(NoContent());
    }

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        _store.TryRemove(id, out _);
        return Task.FromResult<IActionResult>(NoContent());
    }
}