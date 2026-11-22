using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelectureDevis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RevueEtat",
                schema: "devis",
                table: "devis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevueLe",
                schema: "devis",
                table: "devis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevuePar",
                schema: "devis",
                table: "devis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevueRemarque",
                schema: "devis",
                table: "devis",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevueEtat",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "RevueLe",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "RevuePar",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "RevueRemarque",
                schema: "devis",
                table: "devis");
        }
    }
}
