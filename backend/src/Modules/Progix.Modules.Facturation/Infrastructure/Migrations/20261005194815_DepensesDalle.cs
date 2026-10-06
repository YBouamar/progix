using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DepensesDalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categorie",
                schema: "facturation",
                table: "depenses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DalleId",
                schema: "facturation",
                table: "depenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DalleReference",
                schema: "facturation",
                table: "depenses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjetCode",
                schema: "facturation",
                table: "depenses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjetId",
                schema: "facturation",
                table: "depenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categories_depense",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories_depense", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_depenses_ProjetId",
                schema: "facturation",
                table: "depenses",
                column: "ProjetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categories_depense",
                schema: "facturation");

            migrationBuilder.DropIndex(
                name: "IX_depenses_ProjetId",
                schema: "facturation",
                table: "depenses");

            migrationBuilder.DropColumn(
                name: "Categorie",
                schema: "facturation",
                table: "depenses");

            migrationBuilder.DropColumn(
                name: "DalleId",
                schema: "facturation",
                table: "depenses");

            migrationBuilder.DropColumn(
                name: "DalleReference",
                schema: "facturation",
                table: "depenses");

            migrationBuilder.DropColumn(
                name: "ProjetCode",
                schema: "facturation",
                table: "depenses");

            migrationBuilder.DropColumn(
                name: "ProjetId",
                schema: "facturation",
                table: "depenses");
        }
    }
}
