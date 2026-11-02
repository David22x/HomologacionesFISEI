using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentsService.Migrations
{
    /// <inheritdoc />
    public partial class InitialStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Estudiantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cedula = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombres = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModificadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estudiantes", x => x.Id);
                    table.CheckConstraint("CK_Estudiantes_Cedula_Formato", "LEN([Cedula]) = 10 AND [Cedula] NOT LIKE '%[^0-9]%'");
                });

            migrationBuilder.CreateTable(
                name: "HistorialesAcademicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstudianteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarreraOrigenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CarreraOrigenNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InstitucionOrigen = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NivelActual = table.Column<int>(type: "int", nullable: true),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialesAcademicos", x => x.Id);
                    table.CheckConstraint("CK_Historiales_NivelActual_Positivo", "[NivelActual] IS NULL OR [NivelActual] > 0");
                    table.ForeignKey(
                        name: "FK_HistorialesAcademicos_Estudiantes_EstudianteId",
                        column: x => x.EstudianteId,
                        principalTable: "Estudiantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AsignaturasCursadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HistorialAcademicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Creditos = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Nota = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignaturasCursadas", x => x.Id);
                    table.CheckConstraint("CK_AsignaturasCursadas_Creditos_Positivos", "[Creditos] IS NULL OR [Creditos] > 0");
                    table.CheckConstraint("CK_AsignaturasCursadas_Nota_Rango", "[Nota] >= 0 AND [Nota] <= 10");
                    table.ForeignKey(
                        name: "FK_AsignaturasCursadas_HistorialesAcademicos_HistorialAcademicoId",
                        column: x => x.HistorialAcademicoId,
                        principalTable: "HistorialesAcademicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignaturasCursadas_HistorialAcademicoId_AsignaturaId",
                table: "AsignaturasCursadas",
                columns: new[] { "HistorialAcademicoId", "AsignaturaId" },
                unique: true,
                filter: "[AsignaturaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Estudiantes_Cedula",
                table: "Estudiantes",
                column: "Cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialesAcademicos_EstudianteId",
                table: "HistorialesAcademicos",
                column: "EstudianteId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignaturasCursadas");

            migrationBuilder.DropTable(
                name: "HistorialesAcademicos");

            migrationBuilder.DropTable(
                name: "Estudiantes");
        }
    }
}
