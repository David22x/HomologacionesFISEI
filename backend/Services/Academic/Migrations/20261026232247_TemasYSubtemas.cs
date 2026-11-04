using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcademicService.Migrations
{
    /// <inheritdoc />
    public partial class TemasYSubtemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Temas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    HorasEstimadas = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Temas", x => x.Id);
                    table.CheckConstraint("CK_Temas_Horas_Positivas", "[HorasEstimadas] IS NULL OR [HorasEstimadas] > 0");
                    table.CheckConstraint("CK_Temas_Orden_Positivo", "[Orden] > 0");
                    table.ForeignKey(
                        name: "FK_Temas_Asignaturas_AsignaturaId",
                        column: x => x.AsignaturaId,
                        principalTable: "Asignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subtemas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    HorasEstimadas = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subtemas", x => x.Id);
                    table.CheckConstraint("CK_Subtemas_Horas_Positivas", "[HorasEstimadas] IS NULL OR [HorasEstimadas] > 0");
                    table.CheckConstraint("CK_Subtemas_Orden_Positivo", "[Orden] > 0");
                    table.ForeignKey(
                        name: "FK_Subtemas_Temas_TemaId",
                        column: x => x.TemaId,
                        principalTable: "Temas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subtemas_TemaId_Nombre",
                table: "Subtemas",
                columns: new[] { "TemaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Temas_AsignaturaId_Nombre",
                table: "Temas",
                columns: new[] { "AsignaturaId", "Nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subtemas");

            migrationBuilder.DropTable(
                name: "Temas");
        }
    }
}
