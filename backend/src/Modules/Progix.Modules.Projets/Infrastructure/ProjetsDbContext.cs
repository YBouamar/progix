using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Progix.Modules.Projets.Domain;

namespace Progix.Modules.Projets.Infrastructure;

public class ProjetsDbContext(DbContextOptions<ProjetsDbContext> options) : DbContext(options)
{
    public const string Schema = "projets";

    public DbSet<Projet> Projets => Set<Projet>();
    public DbSet<Compteur> Compteurs => Set<Compteur>();
    public DbSet<ProjetPartenaire> ProjetPartenaires => Set<ProjetPartenaire>();
    public DbSet<Dalle> Dalles => Set<Dalle>();
    public DbSet<VersionPlan> VersionsPlans => Set<VersionPlan>();
    public DbSet<RapportEcrasement> RapportsEcrasement => Set<RapportEcrasement>();
    public DbSet<ArticleStock> ArticlesStock => Set<ArticleStock>();
    public DbSet<MouvementStock> MouvementsStock => Set<MouvementStock>();
    public DbSet<Accessoire> Accessoires => Set<Accessoire>();
    public DbSet<CommandeUsine> CommandesUsine => Set<CommandeUsine>();
    public DbSet<FicheInjection> FichesInjection => Set<FicheInjection>();
    public DbSet<FicheAutoControle> FichesAutoControle => Set<FicheAutoControle>();
    public DbSet<ParametresTension> ParametresTension => Set<ParametresTension>();
    public DbSet<ParametresCoulis> ParametresCoulis => Set<ParametresCoulis>();
    public DbSet<MesureTension> MesuresTension => Set<MesureTension>();
    public DbSet<FicheTension> FichesTension => Set<FicheTension>();
    public DbSet<EtapeDalle> Etapes => Set<EtapeDalle>();
    public DbSet<EtapeTrace> EtapeTraces => Set<EtapeTrace>();
    public DbSet<Affectation> Affectations => Set<Affectation>();
    public DbSet<Remarque> Remarques => Set<Remarque>();
    public DbSet<RemarqueTrace> RemarqueTraces => Set<RemarqueTrace>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.Entity<Projet>(b =>
        {
            b.ToTable("projets");
            b.Property(p => p.Code).HasMaxLength(20);
            b.HasIndex(p => p.Code).IsUnique();
            b.Property(p => p.Libelle).HasMaxLength(200);
            b.Property(p => p.Entite).HasMaxLength(5);
            b.Property(p => p.ProprietaireLibre).HasMaxLength(200);
            b.Property(p => p.Adresse).HasMaxLength(300);
            b.Property(p => p.Ville).HasMaxLength(100);
            b.Property(p => p.TypeConstruction).HasMaxLength(20);
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Statut).HasMaxLength(20);
            b.Property(p => p.BlocageMotif).HasMaxLength(500);
            b.Property(p => p.BlocagePar).HasMaxLength(200);
            b.Property(p => p.SuppressionMotif).HasMaxLength(500);
            b.Property(p => p.SuppressionPar).HasMaxLength(200);
            b.Property(p => p.CreePar).HasMaxLength(200);
            b.HasIndex(p => p.ClientId);
            b.HasIndex(p => new { p.Supprime, p.Statut });
        });
        builder.Entity<Dalle>(b =>
        {
            b.ToTable("dalles");
            b.Ignore(d => d.Suffixe);
            b.Property(d => d.Bloc).HasMaxLength(60);
            b.Property(d => d.Immeuble).HasMaxLength(60);
            b.Property(d => d.Detail).HasMaxLength(200);
            b.Property(d => d.DessinateurNom).HasMaxLength(200);
            b.Property(d => d.ConformiteRemarque).HasMaxLength(500);
            b.Property(d => d.ConformitePar).HasMaxLength(200);
            b.Property(d => d.Surface).HasPrecision(12, 2);
            b.HasIndex(d => new { d.ProjetId, d.Numero }).IsUnique();
            b.HasOne<Projet>().WithMany().HasForeignKey(d => d.ProjetId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RapportEcrasement>(b =>
        {
            b.ToTable("rapports_ecrasement");
            b.Property(r => r.Valeur).HasPrecision(7, 2);
            b.Property(r => r.Remarque).HasMaxLength(1000);
            b.Property(r => r.SaisiPar).HasMaxLength(200);
            b.HasIndex(r => new { r.DalleId, r.Numero }).IsUnique();
            b.HasOne<Dalle>().WithMany().HasForeignKey(r => r.DalleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<VersionPlan>(b =>
        {
            b.ToTable("versions_plans");
            b.Property(v => v.Commentaire).HasMaxLength(1000);
            b.Property(v => v.CreePar).HasMaxLength(200);
            b.HasIndex(v => new { v.DalleId, v.Lettre }).IsUnique();
            b.HasOne<Dalle>().WithMany().HasForeignKey(v => v.DalleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<EtapeDalle>(b =>
        {
            b.ToTable("etapes");
            b.Ignore(e => e.Terminee);
            b.Ignore(e => e.Validee);
            b.Property(e => e.FaitPar).HasMaxLength(200);
            b.Property(e => e.Remarque).HasMaxLength(2000);
            b.Property(e => e.ValidationTechniquePar).HasMaxLength(200);
            b.Property(e => e.ValidationAdministrativePar).HasMaxLength(200);
            b.Property(e => e.ValidationChantierPar).HasMaxLength(200);
            b.HasIndex(e => new { e.DalleId, e.Numero }).IsUnique();
            b.HasOne<Dalle>().WithMany().HasForeignKey(e => e.DalleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ArticleStock>(b =>
        {
            b.ToTable("articles_stock");
            b.Property(a => a.Entite).HasMaxLength(5);
            b.Property(a => a.Code).HasMaxLength(50);
            b.Property(a => a.StockInitial).HasPrecision(12, 2);
            b.Property(a => a.Seuil).HasPrecision(12, 2);
            b.HasIndex(a => new { a.Entite, a.Code }).IsUnique();
        });
        builder.Entity<MouvementStock>(b =>
        {
            b.ToTable("mouvements_stock");
            b.Property(m => m.Entite).HasMaxLength(5);
            b.Property(m => m.Code).HasMaxLength(50);
            b.Property(m => m.Sens).HasMaxLength(20);
            b.Property(m => m.Quantite).HasPrecision(12, 2);
            b.Property(m => m.Motif).HasMaxLength(300);
            b.Property(m => m.Par).HasMaxLength(200);
            b.HasIndex(m => new { m.Entite, m.Code });
        });
        builder.Entity<Accessoire>(b =>
        {
            b.ToTable("accessoires");
            b.Property(a => a.Libelle).HasMaxLength(100);
            b.Property(a => a.Unite).HasMaxLength(20);
        });
        builder.Entity<CommandeUsine>(b =>
        {
            b.ToTable("commandes_usine");
            b.HasIndex(c => c.DalleId).IsUnique();
            b.Ignore(c => c.Consommee);
            b.Property(c => c.SuiviPar).HasMaxLength(200);
            b.OwnsMany(c => c.Lignes, l => l.ToJson());
            b.OwnsMany(c => c.Accessoires, a => a.ToJson());
            b.HasOne<Dalle>().WithMany().HasForeignKey(c => c.DalleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<FicheAutoControle>(b =>
        {
            b.ToTable("fiches_auto_controle");
            b.HasIndex(f => f.DalleId).IsUnique();
            b.Property(f => f.SaisiPar).HasMaxLength(200);
            b.OwnsMany(f => f.Points, p => p.ToJson());
        });
        builder.Entity<FicheInjection>(b =>
        {
            b.ToTable("fiches_injection");
            b.HasIndex(f => f.DalleId).IsUnique();
            b.Property(f => f.Malaxeur).HasMaxLength(100);
            b.Property(f => f.Pompe).HasMaxLength(100);
            b.Property(f => f.CimentType).HasMaxLength(100);
            b.Property(f => f.Eau).HasMaxLength(100);
            b.Property(f => f.AdjuvantType).HasMaxLength(100);
            b.Property(f => f.ClientNom).HasMaxLength(200);
            b.Property(f => f.SignePar).HasMaxLength(200);
            foreach (var p in new[] { nameof(FicheInjection.AirAmbiant), nameof(FicheInjection.CimentKg), nameof(FicheInjection.CimentTemperature),
                         nameof(FicheInjection.EauLitres), nameof(FicheInjection.AdjuvantGrammes) })
                b.Property(p).HasPrecision(8, 2);
            b.OwnsMany(f => f.Cables, c => c.ToJson());
            b.HasOne<Dalle>().WithMany().HasForeignKey(f => f.DalleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<FicheTension>(b =>
        {
            b.ToTable("fiches_tension");
            b.HasIndex(f => f.DalleId).IsUnique();
            b.Property(f => f.ForceKn).HasPrecision(8, 2);
            b.Property(f => f.Cr).HasMaxLength(100);
            b.Property(f => f.Cmp).HasMaxLength(100);
        });
        builder.Entity<MesureTension>(b =>
        {
            b.ToTable("mesures_tension");
            b.Property(m => m.Cable).HasMaxLength(20);
            b.Property(m => m.Theorique).HasPrecision(8, 1);
            // Les deux premières lectures reprennent les colonnes de l'ancienne fiche (lecture initiale et finale).
            b.Property(m => m.A25).HasColumnName("LectureInitiale");
            b.Property(m => m.A100).HasColumnName("LectureFinale");
            b.Ignore(m => m.DeltaA); b.Ignore(m => m.DeltaB); b.Ignore(m => m.CoteBIncomplet); b.Ignore(m => m.Min); b.Ignore(m => m.Max); b.Ignore(m => m.Moyenne);
            b.Property(m => m.Longueur).HasPrecision(8, 2);
            foreach (var lecture in new[] { nameof(MesureTension.A25), nameof(MesureTension.A100), nameof(MesureTension.B25), nameof(MesureTension.B100) }) b.Property(lecture).HasPrecision(8, 1);
            b.HasIndex(m => m.DalleId);
            b.HasOne<Dalle>().WithMany().HasForeignKey(m => m.DalleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ParametresCoulis>(b =>
        {
            b.ToTable("parametres_coulis");
            b.Property(x => x.Id).ValueGeneratedNever();
            foreach (var t in new[] { nameof(Domain.ParametresCoulis.CimentType), nameof(Domain.ParametresCoulis.Eau), nameof(Domain.ParametresCoulis.AdjuvantType) }) b.Property(t).HasMaxLength(100);
            b.Property(x => x.ModifiePar).HasMaxLength(200);
        });
        builder.Entity<ParametresTension>(b =>
        {
            b.ToTable("parametres_tension");
            b.Ignore(p => p.MmParMetre);
            b.Property(p => p.Id).ValueGeneratedNever();
            b.Property(p => p.ForceKn).HasPrecision(8, 2);
            b.Property(p => p.SectionMm2).HasPrecision(8, 2);
            b.Property(p => p.ModuleMpa).HasPrecision(10, 2);
            b.Property(p => p.PertesPct).HasPrecision(5, 2);
            b.Property(p => p.ModifiePar).HasMaxLength(200);
        });
        builder.Entity<EtapeTrace>(b =>
        {
            b.ToTable("etape_traces");
            b.Property(e => e.Motif).HasMaxLength(500);
            b.Property(e => e.Par).HasMaxLength(200);
            b.HasOne<Dalle>().WithMany().HasForeignKey(e => e.DalleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Affectation>(b =>
        {
            b.ToTable("affectations");
            b.Property(a => a.Fonction).HasMaxLength(20);
            b.Property(a => a.UserNom).HasMaxLength(200);
            b.HasIndex(a => new { a.ProjetId, a.Fonction });
            b.HasIndex(a => a.UserId);
            b.HasOne<Projet>().WithMany().HasForeignKey(a => a.ProjetId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ProjetPartenaire>(b =>
        {
            b.ToTable("projet_partenaires");
            b.Property(x => x.Role).HasMaxLength(20);
            b.HasIndex(x => new { x.ProjetId, x.Role }).IsUnique();
            b.HasOne<Projet>().WithMany().HasForeignKey(x => x.ProjetId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Remarque>(b =>
        {
            b.ToTable("remarques");
            b.Property(x => x.Texte).HasMaxLength(4000);
            b.Property(x => x.AuteurNom).HasMaxLength(200);
            b.Property(x => x.AuteurRole).HasMaxLength(100);
            b.Property(x => x.ModifiePar).HasMaxLength(200);
            b.Property(x => x.SupprimePar).HasMaxLength(200);
            b.HasIndex(x => x.ProjetId);
            b.HasOne<Projet>().WithMany().HasForeignKey(x => x.ProjetId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Traces).WithOne().HasForeignKey(t => t.RemarqueId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RemarqueTrace>(b =>
        {
            b.ToTable("remarque_traces");
            b.Property(x => x.Action).HasMaxLength(20);
            b.Property(x => x.AncienTexte).HasMaxLength(4000);
            b.Property(x => x.Par).HasMaxLength(200);
        });
        builder.Entity<Compteur>(b =>
        {
            b.ToTable("compteurs");
            b.HasKey(c => c.Cle);
            b.Property(c => c.Cle).HasMaxLength(40);
        });
    }

    /// <summary>Numéro suivant, attribué de façon atomique même en cas de créations simultanées.</summary>
    public async Task<int> NumeroSuivantAsync(string cle, CancellationToken ct = default)
    {
        var r = await Database.SqlQuery<int>($"""
            INSERT INTO projets.compteurs ("Cle", "Valeur") VALUES ({cle}, 1)
            ON CONFLICT ("Cle") DO UPDATE SET "Valeur" = projets.compteurs."Valeur" + 1
            RETURNING "Valeur"
            """).ToListAsync(ct);
        return r[0];
    }
}

public class ProjetsDbContextFactory : IDesignTimeDbContextFactory<ProjetsDbContext>
{
    public ProjetsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ProjetsDbContext>()
            .UseNpgsql("Host=localhost;Database=progix;Username=progix;Password=progix",
                o => o.MigrationsHistoryTable("__ef_migrations", ProjetsDbContext.Schema))
            .Options);
}
