using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Progix.Modules.Identity.Domain;

namespace Progix.Modules.Identity.Infrastructure;

public class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public const string Schema = "identity";

    public DbSet<Habilitation> Habilitations => Set<Habilitation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);
        builder.Entity<AppUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.FullName).HasMaxLength(200);
        });
        builder.Entity<AppRole>(b =>
        {
            b.ToTable("roles");
            b.Property(r => r.Label).HasMaxLength(100);
            b.Property(r => r.Base).HasMaxLength(64);
        });
        builder.Entity<Habilitation>(b =>
        {
            b.ToTable("habilitations");
            b.Property(h => h.Module).HasMaxLength(64);
            b.HasIndex(h => new { h.RoleId, h.Module }).IsUnique();
            b.HasOne<AppRole>().WithMany().HasForeignKey(h => h.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>Utilisé par « dotnet ef » pour générer les migrations sans démarrer l'API.</summary>
public class IdentityModuleDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql("Host=localhost;Database=progix;Username=progix;Password=progix",
                o => o.MigrationsHistoryTable("__ef_migrations", IdentityModuleDbContext.Schema))
            .Options;
        return new IdentityModuleDbContext(options);
    }
}
