using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Progix.Modules.Negoce.Domain;

namespace Progix.Modules.Negoce.Infrastructure;

/// <summary>Ventes directes, achats et stock : un schéma à part, relié aux autres modules par identifiants seulement.</summary>
public class NegoceDbContext(DbContextOptions<NegoceDbContext> options) : DbContext(options)
{
    public const string Schema = "negoce";

    public DbSet<Unite> Unites => Set<Unite>();
    public DbSet<CategorieArticle> Categories => Set<CategorieArticle>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Fournisseur> Fournisseurs => Set<Fournisseur>();
    public DbSet<PrixFournisseur> PrixFournisseurs => Set<PrixFournisseur>();
    public DbSet<GroupeClient> Groupes => Set<GroupeClient>();
    public DbSet<RegleGroupe> ReglesGroupe => Set<RegleGroupe>();
    public DbSet<MembreGroupe> MembresGroupe => Set<MembreGroupe>();
    public DbSet<MouvementStock> Mouvements => Set<MouvementStock>();
    public DbSet<CommandeAchat> CommandesAchat => Set<CommandeAchat>();
    public DbSet<LigneAchat> LignesAchat => Set<LigneAchat>();
    public DbSet<ReceptionAchat> Receptions => Set<ReceptionAchat>();
    public DbSet<LigneReception> LignesReception => Set<LigneReception>();
    public DbSet<FactureFournisseur> FacturesFournisseur => Set<FactureFournisseur>();
    public DbSet<PaiementFournisseur> PaiementsFournisseur => Set<PaiementFournisseur>();
    public DbSet<DocumentVente> Documents => Set<DocumentVente>();
    public DbSet<LigneVente> LignesVente => Set<LigneVente>();
    public DbSet<PaiementClient> PaiementsClient => Set<PaiementClient>();
    public DbSet<CompteurNegoce> Compteurs => Set<CompteurNegoce>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration) =>
        configuration.Properties<decimal>().HavePrecision(14, 4);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.Entity<Unite>(b =>
        {
            b.ToTable("unites");
            b.Property(x => x.Libelle).HasMaxLength(60);
            b.Property(x => x.Symbole).HasMaxLength(12);
            b.HasIndex(x => x.Libelle).IsUnique();
        });
        builder.Entity<CategorieArticle>(b =>
        {
            b.ToTable("categories");
            b.Property(x => x.Libelle).HasMaxLength(80);
            b.HasIndex(x => x.Libelle).IsUnique();
        });
        builder.Entity<Article>(b =>
        {
            b.ToTable("articles");
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Designation).HasMaxLength(200);
            b.Property(x => x.Origine).HasMaxLength(10);
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.VersionStock).IsConcurrencyToken();
            b.HasOne(x => x.Categorie).WithMany().HasForeignKey(x => x.CategorieId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Unite).WithMany().HasForeignKey(x => x.UniteId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Fournisseur>(b =>
        {
            b.ToTable("fournisseurs");
            b.Property(x => x.Nom).HasMaxLength(200);
            b.Property(x => x.Contact).HasMaxLength(200);
            b.Property(x => x.Telephone).HasMaxLength(40);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Adresse).HasMaxLength(300);
            b.Property(x => x.Ville).HasMaxLength(100);
            b.Property(x => x.Ice).HasMaxLength(30);
            b.Property(x => x.Notes).HasMaxLength(1000);
        });
        builder.Entity<PrixFournisseur>(b =>
        {
            b.ToTable("prix_fournisseurs");
            b.HasIndex(x => new { x.ArticleId, x.FournisseurId }).IsUnique();
            b.HasOne<Article>().WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Fournisseur>().WithMany().HasForeignKey(x => x.FournisseurId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<GroupeClient>(b =>
        {
            b.ToTable("groupes_clients");
            b.Property(x => x.Nom).HasMaxLength(120);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => x.Nom).IsUnique();
            b.HasMany(x => x.Regles).WithOne().HasForeignKey(r => r.GroupeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Membres).WithOne().HasForeignKey(m => m.GroupeId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RegleGroupe>(b =>
        {
            b.ToTable("regles_groupe");
            b.Property(x => x.Type).HasMaxLength(10);
            b.HasIndex(x => new { x.GroupeId, x.ArticleId, x.CategorieId }).IsUnique();
        });
        builder.Entity<MembreGroupe>(b =>
        {
            b.ToTable("membres_groupe");
            b.HasKey(x => x.ClientId);
        });
        builder.Entity<MouvementStock>(b =>
        {
            b.ToTable("mouvements");
            b.Property(x => x.Type).HasMaxLength(12);
            b.Property(x => x.Motif).HasMaxLength(500);
            b.Property(x => x.PieceType).HasMaxLength(20);
            b.Property(x => x.PieceNumero).HasMaxLength(40);
            b.Property(x => x.Par).HasMaxLength(200);
            b.HasIndex(x => new { x.ArticleId, x.Date });
            b.HasOne<Article>().WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CommandeAchat>(b =>
        {
            b.ToTable("commandes_achat");
            b.Ignore(x => x.Etat).Ignore(x => x.TotalHT).Ignore(x => x.TotalTVA).Ignore(x => x.TotalTTC).Ignore(x => x.Paye).Ignore(x => x.ResteAPayer);
            b.Property(x => x.Numero).HasMaxLength(20);
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.FournisseurNom).HasMaxLength(200);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.CreePar).HasMaxLength(200);
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.Property(x => x.AnnulePar).HasMaxLength(200);
            b.HasIndex(x => new { x.Entite, x.Numero }).IsUnique();
            b.HasIndex(x => x.FournisseurId);
            b.HasMany(x => x.Lignes).WithOne().HasForeignKey(l => l.CommandeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Receptions).WithOne().HasForeignKey(r => r.CommandeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Factures).WithOne().HasForeignKey(f => f.CommandeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Paiements).WithOne().HasForeignKey(p => p.CommandeId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<LigneAchat>(b =>
        {
            b.ToTable("lignes_achat");
            b.Ignore(x => x.Reste);
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Designation).HasMaxLength(200);
            b.Property(x => x.Unite).HasMaxLength(60);
        });
        builder.Entity<ReceptionAchat>(b =>
        {
            b.ToTable("receptions");
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.Par).HasMaxLength(200);
            b.HasMany(x => x.Lignes).WithOne().HasForeignKey(l => l.ReceptionId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<LigneReception>(b => b.ToTable("lignes_reception"));
        builder.Entity<FactureFournisseur>(b =>
        {
            b.ToTable("factures_fournisseur");
            b.Property(x => x.NumeroFournisseur).HasMaxLength(60);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.Par).HasMaxLength(200);
        });
        builder.Entity<PaiementFournisseur>(b =>
        {
            b.ToTable("paiements_fournisseur");
            b.Property(x => x.Mode).HasMaxLength(12);
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.Par).HasMaxLength(200);
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.Property(x => x.AnnulePar).HasMaxLength(200);
        });
        builder.Entity<DocumentVente>(b =>
        {
            b.ToTable("documents_vente");
            b.Ignore(x => x.Annule).Ignore(x => x.TotalHT).Ignore(x => x.TotalTVA).Ignore(x => x.TotalTTC).Ignore(x => x.Encaisse);
            b.Property(x => x.Type).HasMaxLength(10);
            b.Property(x => x.Numero).HasMaxLength(20);
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.ClientNom).HasMaxLength(200);
            b.Property(x => x.ProjetCode).HasMaxLength(40);
            b.Property(x => x.ProjetLibelle).HasMaxLength(200);
            b.Property(x => x.GroupeNom).HasMaxLength(120);
            b.Property(x => x.ParentNumero).HasMaxLength(20);
            b.Property(x => x.Etat).HasMaxLength(10);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.CreePar).HasMaxLength(200);
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.Property(x => x.AnnulePar).HasMaxLength(200);
            b.HasIndex(x => new { x.Entite, x.Numero }).IsUnique();
            b.HasIndex(x => new { x.Type, x.Etat });
            b.HasIndex(x => x.ParentId);
            b.HasIndex(x => x.ClientId);
            b.HasMany(x => x.Lignes).WithOne().HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Paiements).WithOne().HasForeignKey(p => p.FactureId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<LigneVente>(b =>
        {
            b.ToTable("lignes_vente");
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Designation).HasMaxLength(200);
            b.Property(x => x.Unite).HasMaxLength(60);
            b.HasIndex(x => x.SourceLigneId);
        });
        builder.Entity<PaiementClient>(b =>
        {
            b.ToTable("paiements_client");
            b.Property(x => x.Entite).HasMaxLength(5);
            b.Property(x => x.Mode).HasMaxLength(12);
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.Par).HasMaxLength(200);
            b.Property(x => x.MotifAnnulation).HasMaxLength(500);
            b.Property(x => x.AnnulePar).HasMaxLength(200);
            b.HasIndex(x => x.ClientId);
        });
        builder.Entity<CompteurNegoce>(b =>
        {
            b.ToTable("compteurs");
            b.HasKey(c => c.Cle);
            b.Property(c => c.Cle).HasMaxLength(40);
        });
    }

    /// <summary>Numéro suivant d'un compteur propre au négoce (commandes de vente et d'achat).</summary>
    public async Task<int> NumeroSuivantAsync(string cle, CancellationToken ct = default)
    {
        var r = await Database.SqlQuery<int>($"""
            INSERT INTO negoce.compteurs ("Cle", "Valeur") VALUES ({cle}, 1)
            ON CONFLICT ("Cle") DO UPDATE SET "Valeur" = negoce.compteurs."Valeur" + 1
            RETURNING "Valeur"
            """).ToListAsync(ct);
        return r[0];
    }
}

public class NegoceDbContextFactory : IDesignTimeDbContextFactory<NegoceDbContext>
{
    public NegoceDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NegoceDbContext>()
            .UseNpgsql("Host=localhost;Database=progix;Username=progix;Password=progix",
                o => o.MigrationsHistoryTable("__ef_migrations", NegoceDbContext.Schema))
            .Options);
}
