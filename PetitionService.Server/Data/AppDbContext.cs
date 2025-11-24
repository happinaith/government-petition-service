using Microsoft.EntityFrameworkCore;
using PetitionService.Server.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace PetitionService.Server.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
 public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
 {
 }

 public DbSet<Petition> Petitions => Set<Petition>();
}
