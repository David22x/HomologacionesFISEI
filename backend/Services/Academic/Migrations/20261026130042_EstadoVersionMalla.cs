using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcademicService.Migrations
{
    /// <inheritdoc />
    public partial class EstadoVersionMalla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Mallas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Borrador");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Mallas",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "ModificadoPor",
                table: "Mallas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("UPDATE Mallas SET Estado = 'Publicada' WHERE Vigente = 1");
            migrationBuilder.CreateIndex(
                name: "UX_Mallas_UnaPublicadaPorCarrera",
                table: "Mallas",
                column: "CarreraId",
                unique: true,
                filter: "[Estado] = 'Publicada'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Mallas_UnaPublicadaPorCarrera",
                table: "Mallas");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Mallas");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Mallas");

            migrationBuilder.DropColumn(
                name: "ModificadoPor",
                table: "Mallas");
        }
    }
}
