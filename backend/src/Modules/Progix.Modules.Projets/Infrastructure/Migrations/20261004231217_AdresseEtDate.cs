using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdresseEtDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Adresse",
                schema: "projets",
                table: "projets",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateCreation",
                schema: "projets",
                table: "projets",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "Ville",
                schema: "projets",
                table: "projets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        
            // Les projets existants reprennent leur date d'enregistrement.
            migrationBuilder.Sql("UPDATE projets.projets SET \"DateCreation\" = \"CreatedAt\"::date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Adresse",
                schema: "projets",
                table: "projets");

            migrationBuilder.DropColumn(
                name: "DateCreation",
                schema: "projets",
                table: "projets");

            migrationBuilder.DropColumn(
                name: "Ville",
                schema: "projets",
                table: "projets");
        }
    }
}
