using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Progix.Modules.Facturation.Domain;

namespace Progix.Modules.Facturation.Infrastructure;

public class FacturationDbContext(DbContextOptions<FacturationDbContext> options) : DbContext(options)
{
    public const string Schema = "facturation";

    public DbSet<BonLivraison> BonsLivraison => Set<BonLivraison>();
    public DbSet<LigneLivraison> LignesLivraison => Set<LigneLivraison>();
    public DbSet<Facture> Factures => Set<Facture>();
    public DbSet<LigneFacture> LignesFacture => Set<LigneFacture>();
    public DbSet<Encaissement> Encaissements => Set<Encaissement>();
    public DbSet<Avoir> Avoirs => Set<Avoir>();
    public DbSet<Depense> Depenses => Set<Depense>();
    public DbSet<CategorieDepense> CategoriesDepense => Set<CategorieDepense>();
    public DbSet<ParametresEntite> Entites => Set<ParametresEntite>();
    public DbSet<Contrat> Contrats => Set<Contrat>();
    public DbSet<Courrier> Courriers => Set<Courrier>();
    public DbSet<SignatureFiche> SignaturesFiches => Set<SignatureFiche>();
    public DbSet<Compteur> Compteurs => Set<Compteur>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.Entity<BonLivraison>(b =>
        {
            b.ToTable("bons_livraison");
            b.Property(x => x.Numero).HasMaxLength(20);
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.AncienNumero).HasMaxLength(60);
            b.HasIndex(x => new { x.Entite, x.Numero }).IsUnique();
            b.HasIndex(x => x.ProjetId);
            b.Property(x => x.Surface).HasPrecision(12, 2);
            b.Property(x => x.EmisPar).HasMaxLength(200);
            b.Property(x => x.SignePar).HasMaxLength(200);
            b.Property(x => x.Jeton).HasMaxLength(64);
            b.HasIndex(x => x.Jeton).IsUnique();
            b.HasMany(x => x.Lignes).WithOne().HasForeignKey(l => l.BonLivraisonId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<LigneLivraison>(b =>
        {
            b.ToTable("lignes_livraison");
            b.Property(x => x.Reference).HasMaxLength(30);
            b.Property(x => x.Emplacement).HasMaxLength(200);
            b.Property(x => x.Surface).HasPrecision(12, 2);
            // Garantie en base : une dalle ne peut être livrée qu'une fois.
            b.HasIndex(x => x.DalleId).IsUnique();
        });
        builder.Entity<Facture>(b =>
        {
            b.ToTable("factures");
            b.Property(x => x.Numero).HasMaxLength(20);
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.AncienNumero).HasMaxLength(60);
            b.HasIndex(x => new { x.Entite, x.Numero }).IsUnique();
            b.HasIndex(x => x.ProjetId);
            b.HasIndex(x => x.ClientId);
            b.Property(x => x.Type).HasMaxLength(20);
            b.Property(x => x.ModeReglement).HasMaxLength(20);
            b.Property(x => x.SignePar).HasMaxLength(200);
            b.Property(x => x.Jeton).HasMaxLength(64);
            b.HasIndex(x => x.Jeton).IsUnique();
            b.Property(x => x.ProjetCode).HasMaxLength(20);
            b.Property(x => x.ProjetLibelle).HasMaxLength(200);
            b.Property(x => x.ClientNom).HasMaxLength(200);
            b.Property(x => x.ClientIdentifiants).HasMaxLength(300);
            b.Property(x => x.ClientAdresse).HasMaxLength(400);
            b.Property(x => x.DevisNumero).HasMaxLength(30);
            b.Property(x => x.Origine).HasMaxLength(200);
            b.Property(x => x.EmisPar).HasMaxLength(200);
            foreach (var p in new[] { nameof(Facture.TotalHT), nameof(Facture.AcompteDeduit), nameof(Facture.NetHT), nameof(Facture.Tva), nameof(Facture.TotalTTC) })
                b.Property(p).HasPrecision(14, 2);
            b.Property(x => x.TauxTva).HasPrecision(5, 2);
            b.Property(x => x.AcomptePct).HasPrecision(9, 6);
            b.HasMany(x => x.Lignes).WithOne().HasForeignKey(l => l.FactureId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Encaissements).WithOne().HasForeignKey(e => e.FactureId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Avoirs).WithOne().HasForeignKey(a => a.FactureId).OnDelete(DeleteBehavior.Restrict);
            b.Ignore(x => x.Regle); b.Ignore(x => x.Reste); b.Ignore(x => x.Avoirs_TTC); b.Ignore(x => x.Du); b.Ignore(x => x.Annulee); b.Ignore(x => x.EffetsEnAttente); b.Ignore(x => x.ACouvrir);
        });
        builder.Entity<Avoir>(b =>
        {
            b.ToTable("avoirs");
            b.Property(x => x.Numero).HasMaxLength(20);
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.AncienNumero).HasMaxLength(60);
            b.HasIndex(x => new { x.Entite, x.Numero }).IsUnique();
            b.HasIndex(x => x.ProjetId);
            b.Property(x => x.Motif).HasMaxLength(500);
            b.Property(x => x.SignePar).HasMaxLength(200);
            b.Property(x => x.Jeton).HasMaxLength(64);
            b.HasIndex(x => x.Jeton).IsUnique();
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.Property(x => x.AnnulePar).HasMaxLength(200);
            b.Property(x => x.EmisPar).HasMaxLength(200);
            foreach (var p in new[] { nameof(Avoir.MontantHT), nameof(Avoir.Tva), nameof(Avoir.TotalTTC) }) b.Property(p).HasPrecision(14, 2);
            b.Property(x => x.TauxTva).HasPrecision(5, 2);
        });
        builder.Entity<Encaissement>(b =>
        {
            b.ToTable("encaissements");
            b.Property(x => x.Mode).HasMaxLength(20);
            b.Property(x => x.Montant).HasPrecision(14, 2);
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Banque).HasMaxLength(100);
            b.Property(x => x.SaisiPar).HasMaxLength(200);
            b.Property(x => x.AnnulePar).HasMaxLength(200);
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.HasIndex(x => new { x.ClientId, x.DatePaiement });
            b.Property(x => x.NumeroRecu).HasMaxLength(20);
            b.Property(x => x.AncienNumeroRecu).HasMaxLength(60);
            b.Property(x => x.Jeton).HasMaxLength(64);
            b.HasIndex(x => x.Jeton).IsUnique();
        });
        builder.Entity<LigneFacture>(b =>
        {
            b.ToTable("lignes_facture");
            b.Property(x => x.Libelle).HasMaxLength(300);
            b.Property(x => x.Surface).HasPrecision(12, 2);
            b.Property(x => x.PrixM2).HasPrecision(10, 2);
            b.Property(x => x.MontantHT).HasPrecision(14, 2);
        });
        builder.Entity<Depense>(b =>
        {
            b.ToTable("depenses");
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.Libelle).HasMaxLength(200);
            b.Property(x => x.Montant).HasPrecision(14, 2);
            b.Property(x => x.Mode).HasMaxLength(20);
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.SaisiPar).HasMaxLength(200);
            b.Property(x => x.AnnulePar).HasMaxLength(200);
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.HasIndex(x => x.DateDepense);
            b.HasIndex(x => x.ProjetId);
            b.Property(x => x.ProjetCode).HasMaxLength(20);
            b.Property(x => x.DalleReference).HasMaxLength(30);
            b.Property(x => x.Categorie).HasMaxLength(100);
        });
        builder.Entity<ParametresEntite>(b =>
        {
            b.ToTable("entites");
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Code).HasMaxLength(5);
            b.Property(x => x.RaisonSociale).HasMaxLength(200);
            b.Property(x => x.Capital).HasPrecision(16, 2);
            b.Property(x => x.Siege).HasMaxLength(300);
            foreach (var p in new[] { nameof(ParametresEntite.FormeJuridique), nameof(ParametresEntite.Ville), nameof(ParametresEntite.Telephone), nameof(ParametresEntite.Email),
                nameof(ParametresEntite.Ice), nameof(ParametresEntite.IdentifiantFiscal), nameof(ParametresEntite.Rc), nameof(ParametresEntite.TribunalRc), nameof(ParametresEntite.Patente),
                nameof(ParametresEntite.Cnss), nameof(ParametresEntite.Banque), nameof(ParametresEntite.Rib), nameof(ParametresEntite.LogoType), nameof(ParametresEntite.ModifiePar) })
                b.Property(p).HasMaxLength(100);
        });
        builder.Entity<Contrat>(b =>
        {
            b.ToTable("contrats");
            b.Property(x => x.Numero).HasMaxLength(20);
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.AncienNumero).HasMaxLength(60);
            b.HasIndex(x => new { x.Entite, x.Numero }).IsUnique();
            b.HasIndex(x => x.ProjetId);
            b.Property(x => x.DevisNumero).HasMaxLength(30);
            b.Property(x => x.Lieu).HasMaxLength(100);
            foreach (var p in new[] { nameof(Contrat.EmisPar), nameof(Contrat.SignePar), nameof(Contrat.AnnulePar) }) b.Property(p).HasMaxLength(200);
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.Property(x => x.Jeton).HasMaxLength(64);
            b.HasIndex(x => x.Jeton).IsUnique();
        });
        builder.Entity<Courrier>(b =>
        {
            b.ToTable("courriers");
            b.Property(x => x.Numero).HasMaxLength(20);
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.AncienNumero).HasMaxLength(60);
            // Un numéro est unique par type et par bureau destinataire, toutes entités confondues, parmi les courriers non annulés.
            b.HasIndex(x => new { x.Type, x.DestinataireId, x.Numero }).IsUnique().HasFilter("\"Annule\" = false");
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.Property(x => x.AnnulePar).HasMaxLength(200);
            b.HasIndex(x => x.ProjetId);
            b.Property(x => x.Type).HasMaxLength(20);
            b.Property(x => x.Lieu).HasMaxLength(100);
            b.Property(x => x.Objet).HasMaxLength(500);
            foreach (var p in new[] { nameof(Courrier.Entreprise), nameof(Courrier.Ville), nameof(Courrier.Bct), nameof(Courrier.Bet), nameof(Courrier.EmisPar), nameof(Courrier.SignePar) }) b.Property(p).HasMaxLength(200);
            b.Property(x => x.Jeton).HasMaxLength(64);
            b.HasIndex(x => x.Jeton).IsUnique();
            b.OwnsMany(x => x.Lignes, l => l.ToJson());
        });
        builder.Entity<SignatureFiche>(b =>
        {
            b.ToTable("signatures_fiches");
            b.HasIndex(x => new { x.DalleId, x.Type }).IsUnique();
            b.HasIndex(x => x.Jeton).IsUnique();
            b.Property(x => x.Type).HasMaxLength(20);
            b.Property(x => x.Reference).HasMaxLength(30);
            b.Property(x => x.Titre).HasMaxLength(100);
            b.Property(x => x.SignePar).HasMaxLength(200);
            b.Property(x => x.Jeton).HasMaxLength(64);
        });
        builder.Entity<CategorieDepense>(b =>
        {
            b.ToTable("categories_depense");
            b.Property(x => x.Libelle).HasMaxLength(100);
        });
        builder.Entity<Compteur>(b =>
        {
            b.ToTable("compteurs");
            b.HasKey(c => c.Cle);
            b.Property(c => c.Cle).HasMaxLength(40);
        });
    }

    public Task RecalerAsync(string cle, int minimum, CancellationToken ct = default) =>
        Database.ExecuteSqlAsync($"""
            INSERT INTO facturation.compteurs ("Cle", "Valeur") VALUES ({cle}, {minimum})
            ON CONFLICT ("Cle") DO UPDATE SET "Valeur" = GREATEST(facturation.compteurs."Valeur", {minimum})
            """, ct);

    public async Task<int> NumeroSuivantAsync(string cle, CancellationToken ct = default)
    {
        var r = await Database.SqlQuery<int>($"""
            INSERT INTO facturation.compteurs ("Cle", "Valeur") VALUES ({cle}, 1)
            ON CONFLICT ("Cle") DO UPDATE SET "Valeur" = facturation.compteurs."Valeur" + 1
            RETURNING "Valeur"
            """).ToListAsync(ct);
        return r[0];
    }
}

public class FacturationDbContextFactory : IDesignTimeDbContextFactory<FacturationDbContext>
{
    public FacturationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FacturationDbContext>()
            .UseNpgsql("Host=localhost;Database=progix;Username=progix;Password=progix",
                o => o.MigrationsHistoryTable("__ef_migrations", FacturationDbContext.Schema))
            .Options);
}
