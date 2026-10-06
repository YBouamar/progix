using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Progix.Modules.Partenaires.Domain;

namespace Progix.Modules.Partenaires.Infrastructure;

public class PartenairesDbContext(DbContextOptions<PartenairesDbContext> options) : DbContext(options)
{
    public const string Schema = "partenaires";

    public DbSet<Partenaire> Partenaires => Set<Partenaire>();
    public DbSet<Contact> Contacts => Set<Contact>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.Entity<Partenaire>(b =>
        {
            b.ToTable("partenaires");
            b.Ignore(p => p.NomAffiche);
            b.Property(p => p.Type).HasMaxLength(30);
            b.Property(p => p.Nature).HasMaxLength(10);
            b.Property(p => p.RaisonSociale).HasMaxLength(200);
            b.Property(p => p.Ice).HasMaxLength(20);
            b.Property(p => p.Rc).HasMaxLength(50);
            b.Property(p => p.IdentifiantFiscal).HasMaxLength(30);
            b.Property(p => p.Patente).HasMaxLength(30);
            b.Property(p => p.Civilite).HasMaxLength(10);
            b.Property(p => p.Nom).HasMaxLength(100);
            b.Property(p => p.Prenom).HasMaxLength(100);
            b.Property(p => p.Cin).HasMaxLength(20);
            b.Property(p => p.Nationalite).HasMaxLength(60);
            b.Property(p => p.LieuNaissance).HasMaxLength(100);
            b.Property(p => p.Adresse).HasMaxLength(300);
            b.Property(p => p.Ville).HasMaxLength(100);
            b.Property(p => p.Telephone).HasMaxLength(30);
            b.Property(p => p.Email).HasMaxLength(200);
            // Contrôle de doublon : un ICE ou une CIN ne peut exister qu'une fois.
            b.HasIndex(p => p.Ice).IsUnique().HasFilter("\"Ice\" IS NOT NULL");
            b.HasIndex(p => p.Cin).IsUnique().HasFilter("\"Cin\" IS NOT NULL");
            b.HasIndex(p => p.Type);
            b.HasMany(p => p.Contacts).WithOne().HasForeignKey(c => c.PartenaireId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Contact>(b =>
        {
            b.ToTable("contacts");
            b.Property(c => c.Nom).HasMaxLength(200);
            b.Property(c => c.Fonction).HasMaxLength(100);
            b.Property(c => c.Telephone).HasMaxLength(30);
            b.Property(c => c.Email).HasMaxLength(200);
        });
    }
}

public class PartenairesDbContextFactory : IDesignTimeDbContextFactory<PartenairesDbContext>
{
    public PartenairesDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PartenairesDbContext>()
            .UseNpgsql("Host=localhost;Database=progix;Username=progix;Password=progix",
                o => o.MigrationsHistoryTable("__ef_migrations", PartenairesDbContext.Schema))
            .Options);
}
