using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetitionService.Server.Data;
using PetitionService.Server.Models;
using Microsoft.AspNetCore.Authorization;

namespace PetitionService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PetitionsController : ControllerBase
{
 private readonly AppDbContext _db;
 public PetitionsController(AppDbContext db) => _db = db;

 [HttpGet]
 [AllowAnonymous]
 public async Task<ActionResult<IEnumerable<Petition>>> GetAll([FromQuery] string? category)
 {
 var query = _db.Petitions.AsQueryable();
 if (!string.IsNullOrWhiteSpace(category))
 query = query.Where(p => p.Category == category);
 var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
 return Ok(list);
 }

 [HttpGet("{id:int}")]
 [AllowAnonymous]
 public async Task<ActionResult<Petition>> Get(int id)
 {
 var entity = await _db.Petitions.FindAsync(id);
 return entity is null ? NotFound() : Ok(entity);
 }

 [HttpPost]
 public async Task<ActionResult<Petition>> Create([FromBody] Petition petition)
 {
 petition.Id =0; // ensure new
 petition.CreatedAt = DateTime.UtcNow;
 petition.Author = User.Identity?.Name ?? "anonymous";
 _db.Petitions.Add(petition);
 await _db.SaveChangesAsync();
 return CreatedAtAction(nameof(Get), new { id = petition.Id }, petition);
 }

 [HttpPut("{id:int}")]
 public async Task<ActionResult> Update(int id, [FromBody] Petition petition)
 {
 if (id != petition.Id) return BadRequest();
 var exists = await _db.Petitions.AnyAsync(p => p.Id == id);
 if (!exists) return NotFound();
 petition.Author = User.Identity?.Name ?? petition.Author; // preserve
 _db.Entry(petition).State = EntityState.Modified;
 await _db.SaveChangesAsync();
 return NoContent();
 }

 [HttpDelete("{id:int}")]
 public async Task<ActionResult> Delete(int id)
 {
 var entity = await _db.Petitions.FindAsync(id);
 if (entity is null) return NotFound();
 _db.Petitions.Remove(entity);
 await _db.SaveChangesAsync();
 return NoContent();
 }

 [HttpPost("{id:int}/sign")] 
 [AllowAnonymous]
 public async Task<ActionResult<Petition>> Sign(int id)
 {
 var entity = await _db.Petitions.FindAsync(id);
 if (entity is null) return NotFound();
 entity.Signatures +=1;
 await _db.SaveChangesAsync();
 return Ok(entity);
 }
}
