using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Negoce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Negoce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "negoce");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "commandes_achat",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    FournisseurId = table.Column<Guid>(type: "uuid", nullable: false),
                    FournisseurNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateCommande = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Annule = table.Column<bool>(type: "boolean", nullable: false),
                    MotifAnnulation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnnulePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AnnuleLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commandes_achat", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "compteurs",
                schema: "negoce",
                columns: table => new
                {
                    Cle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Valeur = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compteurs", x => x.Cle);
                });

            migrationBuilder.CreateTable(
                name: "documents_vente",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjetCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProjetLibelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GroupeId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupeNom = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentNumero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DateDocument = table.Column<DateOnly>(type: "date", nullable: false),
                    Echeance = table.Column<DateOnly>(type: "date", nullable: true),
                    Etat = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TauxTva = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MotifAnnulation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnnulePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AnnuleLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents_vente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fournisseurs",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Telephone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Adresse = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Ville = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ice = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fournisseurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "groupes_clients",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groupes_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "unites",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Symbole = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "factures_fournisseur",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroFournisseur = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DateFacture = table.Column<DateOnly>(type: "date", nullable: false),
                    MontantTTC = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Par = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factures_fournisseur", x => x.Id);
                    table.ForeignKey(
                        name: "FK_factures_fournisseur_commandes_achat_CommandeId",
                        column: x => x.CommandeId,
                        principalSchema: "negoce",
                        principalTable: "commandes_achat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lignes_achat",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unite = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    QuantiteRecue = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lignes_achat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lignes_achat_commandes_achat_CommandeId",
                        column: x => x.CommandeId,
                        principalSchema: "negoce",
                        principalTable: "commandes_achat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paiements_fournisseur",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatePaiement = table.Column<DateOnly>(type: "date", nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Mode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Par = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Annule = table.Column<bool>(type: "boolean", nullable: false),
                    MotifAnnulation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnnulePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AnnuleLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paiements_fournisseur", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paiements_fournisseur_commandes_achat_CommandeId",
                        column: x => x.CommandeId,
                        principalSchema: "negoce",
                        principalTable: "commandes_achat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receptions",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateReception = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Par = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_receptions_commandes_achat_CommandeId",
                        column: x => x.CommandeId,
                        principalSchema: "negoce",
                        principalTable: "commandes_achat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lignes_vente",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unite = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    SourceLigneId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoutUnitaire = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lignes_vente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lignes_vente_documents_vente_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "negoce",
                        principalTable: "documents_vente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paiements_client",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DatePaiement = table.Column<DateOnly>(type: "date", nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Mode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Par = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Annule = table.Column<bool>(type: "boolean", nullable: false),
                    MotifAnnulation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnnulePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AnnuleLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paiements_client", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paiements_client_documents_vente_FactureId",
                        column: x => x.FactureId,
                        principalSchema: "negoce",
                        principalTable: "documents_vente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "membres_groupe",
                schema: "negoce",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membres_groupe", x => x.ClientId);
                    table.ForeignKey(
                        name: "FK_membres_groupe_groupes_clients_GroupeId",
                        column: x => x.GroupeId,
                        principalSchema: "negoce",
                        principalTable: "groupes_clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regles_groupe",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategorieId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Valeur = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regles_groupe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regles_groupe_groupes_clients_GroupeId",
                        column: x => x.GroupeId,
                        principalSchema: "negoce",
                        principalTable: "groupes_clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "articles",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategorieId = table.Column<Guid>(type: "uuid", nullable: false),
                    UniteId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrixVente = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Origine = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CoutMoyen = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    SeuilAlerte = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    KgParMetre = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_articles_categories_CategorieId",
                        column: x => x.CategorieId,
                        principalSchema: "negoce",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_articles_unites_UniteId",
                        column: x => x.UniteId,
                        principalSchema: "negoce",
                        principalTable: "unites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lignes_reception",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LigneAchatId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lignes_reception", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lignes_reception_receptions_ReceptionId",
                        column: x => x.ReceptionId,
                        principalSchema: "negoce",
                        principalTable: "receptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mouvements",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CoutUnitaire = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Motif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PieceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PieceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PieceNumero = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Par = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mouvements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mouvements_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalSchema: "negoce",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prix_fournisseurs",
                schema: "negoce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FournisseurId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrixAchat = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prix_fournisseurs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prix_fournisseurs_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalSchema: "negoce",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prix_fournisseurs_fournisseurs_FournisseurId",
                        column: x => x.FournisseurId,
                        principalSchema: "negoce",
                        principalTable: "fournisseurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_articles_CategorieId",
                schema: "negoce",
                table: "articles",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_articles_Code",
                schema: "negoce",
                table: "articles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_articles_UniteId",
                schema: "negoce",
                table: "articles",
                column: "UniteId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_Libelle",
                schema: "negoce",
                table: "categories",
                column: "Libelle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commandes_achat_Entite_Numero",
                schema: "negoce",
                table: "commandes_achat",
                columns: new[] { "Entite", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commandes_achat_FournisseurId",
                schema: "negoce",
                table: "commandes_achat",
                column: "FournisseurId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_vente_ClientId",
                schema: "negoce",
                table: "documents_vente",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_vente_Entite_Numero",
                schema: "negoce",
                table: "documents_vente",
                columns: new[] { "Entite", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_vente_ParentId",
                schema: "negoce",
                table: "documents_vente",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_vente_Type_Etat",
                schema: "negoce",
                table: "documents_vente",
                columns: new[] { "Type", "Etat" });

            migrationBuilder.CreateIndex(
                name: "IX_factures_fournisseur_CommandeId",
                schema: "negoce",
                table: "factures_fournisseur",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_groupes_clients_Nom",
                schema: "negoce",
                table: "groupes_clients",
                column: "Nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lignes_achat_CommandeId",
                schema: "negoce",
                table: "lignes_achat",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_lignes_reception_ReceptionId",
                schema: "negoce",
                table: "lignes_reception",
                column: "ReceptionId");

            migrationBuilder.CreateIndex(
                name: "IX_lignes_vente_DocumentId",
                schema: "negoce",
                table: "lignes_vente",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_lignes_vente_SourceLigneId",
                schema: "negoce",
                table: "lignes_vente",
                column: "SourceLigneId");

            migrationBuilder.CreateIndex(
                name: "IX_membres_groupe_GroupeId",
                schema: "negoce",
                table: "membres_groupe",
                column: "GroupeId");

            migrationBuilder.CreateIndex(
                name: "IX_mouvements_ArticleId_Date",
                schema: "negoce",
                table: "mouvements",
                columns: new[] { "ArticleId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_paiements_client_ClientId",
                schema: "negoce",
                table: "paiements_client",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_paiements_client_FactureId",
                schema: "negoce",
                table: "paiements_client",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_paiements_fournisseur_CommandeId",
                schema: "negoce",
                table: "paiements_fournisseur",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_prix_fournisseurs_ArticleId_FournisseurId",
                schema: "negoce",
                table: "prix_fournisseurs",
                columns: new[] { "ArticleId", "FournisseurId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prix_fournisseurs_FournisseurId",
                schema: "negoce",
                table: "prix_fournisseurs",
                column: "FournisseurId");

            migrationBuilder.CreateIndex(
                name: "IX_receptions_CommandeId",
                schema: "negoce",
                table: "receptions",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_regles_groupe_GroupeId_ArticleId_CategorieId",
                schema: "negoce",
                table: "regles_groupe",
                columns: new[] { "GroupeId", "ArticleId", "CategorieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unites_Libelle",
                schema: "negoce",
                table: "unites",
                column: "Libelle",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compteurs",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "factures_fournisseur",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "lignes_achat",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "lignes_reception",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "lignes_vente",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "membres_groupe",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "mouvements",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "paiements_client",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "paiements_fournisseur",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "prix_fournisseurs",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "regles_groupe",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "receptions",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "documents_vente",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "articles",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "fournisseurs",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "groupes_clients",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "commandes_achat",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "negoce");

            migrationBuilder.DropTable(
                name: "unites",
                schema: "negoce");
        }
    }
}
