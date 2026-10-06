using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Progix.Modules.Devis.Domain;

namespace Progix.Modules.Devis.Infrastructure;

public class DevisDbContext(DbContextOptions<DevisDbContext> options) : DbContext(options)
{
    public const string Schema = "devis";

    public DbSet<DevisEntete> Devis => Set<DevisEntete>();
    public DbSet<LigneDevis> Lignes => Set<LigneDevis>();
    public DbSet<EvenementDevis> Evenements => Set<EvenementDevis>();
    public DbSet<CompteurDevis> Compteurs => Set<CompteurDevis>();
    public DbSet<ParametresDevis> Parametres => Set<ParametresDevis>();

    /// <summary>Paramètres en vigueur ; sans réglage enregistré, les valeurs d'origine.</summary>
    public async Task<ParametresDevis> ParametresAsync(CancellationToken ct = default) =>
        await Parametres.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ParametresDevis { Modalites = ParametresDevis.ModalitesParDefaut, Echeances = ParametresDevis.EcheancesParDefaut() };

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.Entity<DevisEntete>(b =>
        {
            b.ToTable("devis");
            b.Ignore(d => d.Echeance);
            b.Property(d => d.Numero).HasMaxLength(20);
            b.Property(d => d.Entite).HasMaxLength(5);
            b.HasIndex(d => new { d.Entite, d.Numero, d.Version }).IsUnique();
            b.HasIndex(d => d.ProjetId);
            b.Property(d => d.Etat).HasMaxLength(20);
            b.Property(d => d.SignePar).HasMaxLength(200);
            b.Property(d => d.Jeton).HasMaxLength(64);
            b.HasIndex(d => d.Jeton).IsUnique();
            b.Property(d => d.CommercialNom).HasMaxLength(200);
            b.Property(d => d.RemiseMotif).HasMaxLength(500);
            b.Property(d => d.ModalitesContrat).HasMaxLength(4000);
            b.Property(d => d.CreePar).HasMaxLength(200);
            foreach (var p in new[] { nameof(DevisEntete.RemiseM2), nameof(DevisEntete.Forfait), nameof(DevisEntete.SurfaceTotale),
                         nameof(DevisEntete.TotalHT), nameof(DevisEntete.RemiseTotale), nameof(DevisEntete.NetHT), nameof(DevisEntete.Tva), nameof(DevisEntete.TotalTTC) })
                b.Property(p).HasPrecision(14, 2);
            b.OwnsMany(d => d.Echeances, e => e.ToJson());
            b.Property(d => d.ValidePar).HasMaxLength(200);
            b.Property(d => d.AnnuleMotif).HasMaxLength(500);
            b.HasMany(d => d.Evenements).WithOne().HasForeignKey(e => e.DevisId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(d => d.Lignes).WithOne().HasForeignKey(l => l.DevisId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<LigneDevis>(b =>
        {
            b.ToTable("lignes");
            b.Property(l => l.Reference).HasMaxLength(30);
            b.Property(l => l.Emplacement).HasMaxLength(200);
            b.Property(l => l.Surface).HasPrecision(12, 2);
            b.Property(l => l.RatioActif).HasPrecision(8, 3);
            b.Property(l => l.RatioPassif).HasPrecision(8, 3);
            b.Property(l => l.MontantHT).HasPrecision(14, 2);
            b.OwnsMany(l => l.Systemes, s => s.ToJson());
        });
        builder.Entity<EvenementDevis>(b =>
        {
            b.ToTable("evenements");
            b.Property(e => e.Quoi).HasMaxLength(600);
            b.Property(e => e.Qui).HasMaxLength(200);
        });
        builder.Entity<ParametresDevis>(b =>
        {
            b.ToTable("parametres");
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Modalites).HasMaxLength(4000);
            b.Property(x => x.ModifiePar).HasMaxLength(200);
            b.OwnsMany(x => x.Echeances, e => e.ToJson());
        });
        builder.Entity<CompteurDevis>(b =>
        {
            b.ToTable("compteurs");
            b.HasKey(c => c.Cle);
            b.Property(c => c.Cle).HasMaxLength(40);
        });
    }

    /// <summary>Un devis envoyé dont l'échéance est passée devient expiré.</summary>
    public Task ExpirerAsync(CancellationToken ct = default)
    {
        var aujourdhui = DateOnly.FromDateTime(DateTime.UtcNow);
        return Database.ExecuteSqlAsync($"""
            UPDATE devis.devis SET "Etat" = 'expire' WHERE "Etat" = 'envoye' AND "DateDevis" + "DureeValidite" < {aujourdhui}
            """, ct);
    }

    /// <summary>Recale un compteur sur le plus grand numéro déjà attribué, sans jamais le faire reculer.</summary>
    public Task RecalerAsync(string cle, int minimum, CancellationToken ct = default) =>
        Database.ExecuteSqlAsync($"""
            INSERT INTO devis.compteurs ("Cle", "Valeur") VALUES ({cle}, {minimum})
            ON CONFLICT ("Cle") DO UPDATE SET "Valeur" = GREATEST(devis.compteurs."Valeur", {minimum})
            """, ct);

    public async Task<int> NumeroSuivantAsync(string cle, CancellationToken ct = default)
    {
        var r = await Database.SqlQuery<int>($"""
            INSERT INTO devis.compteurs ("Cle", "Valeur") VALUES ({cle}, 1)
            ON CONFLICT ("Cle") DO UPDATE SET "Valeur" = devis.compteurs."Valeur" + 1
            RETURNING "Valeur"
            """).ToListAsync(ct);
        return r[0];
    }
}

public class DevisDbContextFactory : IDesignTimeDbContextFactory<DevisDbContext>
{
    public DevisDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DevisDbContext>()
            .UseNpgsql("Host=localhost;Database=progix;Username=progix;Password=progix",
                o => o.MigrationsHistoryTable("__ef_migrations", DevisDbContext.Schema))
            .Options);
}
