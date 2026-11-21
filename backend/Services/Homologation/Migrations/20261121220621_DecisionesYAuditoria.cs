using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologationService.Migrations
{
    /// <inheritdoc />
    public partial class DecisionesYAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Decisiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaDestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrigenIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultadoCalculado = table.Column<int>(type: "int", nullable: false),
                    ResultadoFinal = table.Column<int>(type: "int", nullable: false),
                    Justificacion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProfesorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistradoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decisiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Decisiones_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CambiosJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosAuditoria_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Decisiones_ExpedienteId_AsignaturaDestinoId",
                table: "Decisiones",
                columns: new[] { "ExpedienteId", "AsignaturaDestinoId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosAuditoria_ExpedienteId_Fecha",
                table: "EventosAuditoria",
                columns: new[] { "ExpedienteId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Decisiones");

            migrationBuilder.DropTable(
                name: "EventosAuditoria");
        }
    }
}
