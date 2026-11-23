using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportsService.Migrations
{
    /// <inheritdoc />
    public partial class InitialReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Informes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    GeneradoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneradoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HuellaSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HuellaFuente = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DatosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Informes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Informes_ExpedienteId_HuellaFuente",
                table: "Informes",
                columns: new[] { "ExpedienteId", "HuellaFuente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Informes_ExpedienteId_Version",
                table: "Informes",
                columns: new[] { "ExpedienteId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Informes");
        }
    }
}
