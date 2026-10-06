using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Partenaires.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AttestationExoneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AttestationExpiration",
                schema: "partenaires",
                table: "partenaires",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttestationNumero",
                schema: "partenaires",
                table: "partenaires",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttestationExpiration",
                schema: "partenaires",
                table: "partenaires");

            migrationBuilder.DropColumn(
                name: "AttestationNumero",
                schema: "partenaires",
                table: "partenaires");
        }
    }
}
