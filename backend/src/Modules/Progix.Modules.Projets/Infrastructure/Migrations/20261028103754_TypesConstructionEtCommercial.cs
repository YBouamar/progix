using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TypesConstructionEtCommercial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TypeConstruction",
                schema: "projets",
                table: "projets",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<Guid>(
                name: "CommercialId",
                schema: "projets",
                table: "projets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommercialNom",
                schema: "projets",
                table: "projets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CommercialRepris",
                schema: "projets",
                table: "projets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "types_construction",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_types_construction", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_types_construction_Code",
                schema: "projets",
                table: "types_construction",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "types_construction",
                schema: "projets");

            migrationBuilder.DropColumn(
                name: "CommercialId",
                schema: "projets",
                table: "projets");

            migrationBuilder.DropColumn(
                name: "CommercialNom",
                schema: "projets",
                table: "projets");

            migrationBuilder.DropColumn(
                name: "CommercialRepris",
                schema: "projets",
                table: "projets");

            migrationBuilder.AlterColumn<string>(
                name: "TypeConstruction",
                schema: "projets",
                table: "projets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);
        }
    }
}
