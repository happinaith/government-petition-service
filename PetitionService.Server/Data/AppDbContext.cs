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
 public DbSet<PetitionSignature> PetitionSignatures => Set<PetitionSignature>();
 public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
 public DbSet<UserSecurityState> UserSecurityStates => Set<UserSecurityState>();

 protected override void OnModelCreating(ModelBuilder builder)
 {
	 base.OnModelCreating(builder);

	 builder.Entity<UserSecurityState>()
		 .HasKey(x => x.UserId);

	 builder.Entity<RefreshToken>()
		 .HasIndex(x => x.TokenHash)
		 .IsUnique();

	 builder.Entity<RefreshToken>()
		 .HasIndex(x => new { x.UserId, x.ExpiresAt });
 }
}
