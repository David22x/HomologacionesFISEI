using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologationService.Migrations
{
    /// <inheritdoc />
    public partial class ParesYEjecucionesReglas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EjecucionesReglas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfesorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EjecutadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResultadosJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionesReglas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EjecucionesReglas_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParesHomologacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaCursadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaDestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Precargado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParesHomologacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParesHomologacion_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesReglas_ExpedienteId_EjecutadoEn",
                table: "EjecucionesReglas",
                columns: new[] { "ExpedienteId", "EjecutadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_ParesHomologacion_ExpedienteId_AsignaturaCursadaId_AsignaturaDestinoId",
                table: "ParesHomologacion",
                columns: new[] { "ExpedienteId", "AsignaturaCursadaId", "AsignaturaDestinoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EjecucionesReglas");

            migrationBuilder.DropTable(
                name: "ParesHomologacion");
        }
    }
}
