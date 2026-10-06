using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TensionEnteteEtEtude : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EtudePlanifiee",
                schema: "projets",
                table: "dalles",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fiches_tension",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForceKn = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Cr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Cmp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiches_tension", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fiches_tension_DalleId",
                schema: "projets",
                table: "fiches_tension",
                column: "DalleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiches_tension",
                schema: "projets");

            migrationBuilder.DropColumn(
                name: "EtudePlanifiee",
                schema: "projets",
                table: "dalles");
        }
    }
}
