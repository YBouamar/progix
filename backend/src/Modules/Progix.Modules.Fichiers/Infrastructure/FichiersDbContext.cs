using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Progix.Modules.Fichiers.Domain;

namespace Progix.Modules.Fichiers.Infrastructure;

public class FichiersDbContext(DbContextOptions<FichiersDbContext> options) : DbContext(options)
{
    public const string Schema = "fichiers";

    public DbSet<Fichier> Fichiers => Set<Fichier>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.Entity<Fichier>(b =>
        {
            b.ToTable("fichiers");
            b.Property(f => f.Contexte).HasMaxLength(30);
            b.Property(f => f.Categorie).HasMaxLength(30);
            b.Property(f => f.Nom).HasMaxLength(255);
            b.Property(f => f.ContentType).HasMaxLength(150);
            b.Property(f => f.Cle).HasMaxLength(300);
            b.Property(f => f.AjoutePar).HasMaxLength(200);
            b.Property(f => f.SupprimePar).HasMaxLength(200);
            b.Property(f => f.EnvoiId).HasMaxLength(300);
            b.HasIndex(f => new { f.Contexte, f.CibleId });
        });
    }
}

public class FichiersDbContextFactory : IDesignTimeDbContextFactory<FichiersDbContext>
{
    public FichiersDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FichiersDbContext>()
            .UseNpgsql("Host=localhost;Database=progix;Username=progix;Password=progix",
                o => o.MigrationsHistoryTable("__ef_migrations", FichiersDbContext.Schema))
            .Options);
}
