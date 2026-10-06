using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignaturesFiches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "signatures_fiches",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Titre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SignePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SigneLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Jeton = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signatures_fiches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_signatures_fiches_DalleId_Type",
                schema: "facturation",
                table: "signatures_fiches",
                columns: new[] { "DalleId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signatures_fiches_Jeton",
                schema: "facturation",
                table: "signatures_fiches",
                column: "Jeton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "signatures_fiches",
                schema: "facturation");
        }
    }
}
