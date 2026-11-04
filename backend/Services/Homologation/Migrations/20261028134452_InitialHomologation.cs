using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialHomologation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Expedientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstudianteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarreraDestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MallaDestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProfesorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfesorNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expedientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equivalencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaDestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equivalencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equivalencias_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquivalenciasOrigen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquivalenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaCursadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquivalenciasOrigen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquivalenciasOrigen_Equivalencias_EquivalenciaId",
                        column: x => x.EquivalenciaId,
                        principalTable: "Equivalencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equivalencias_ExpedienteId_AsignaturaDestinoId",
                table: "Equivalencias",
                columns: new[] { "ExpedienteId", "AsignaturaDestinoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquivalenciasOrigen_EquivalenciaId_AsignaturaCursadaId",
                table: "EquivalenciasOrigen",
                columns: new[] { "EquivalenciaId", "AsignaturaCursadaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_ProfesorId",
                table: "Expedientes",
                column: "ProfesorId");

            migrationBuilder.CreateIndex(
                name: "UX_Expedientes_UnoActivoPorEstudianteYCarrera",
                table: "Expedientes",
                columns: new[] { "EstudianteId", "CarreraDestinoId" },
                unique: true,
                filter: "[Estado] IN ('Borrador','EnRevision')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquivalenciasOrigen");

            migrationBuilder.DropTable(
                name: "Equivalencias");

            migrationBuilder.DropTable(
                name: "Expedientes");
        }
    }
}
