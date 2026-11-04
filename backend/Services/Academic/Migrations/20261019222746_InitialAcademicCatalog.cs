using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcademicService.Migrations
{
    /// <inheritdoc />
    public partial class InitialAcademicCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Carreras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carreras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mallas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mallas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mallas_Carreras_CarreraId",
                        column: x => x.CarreraId,
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Niveles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MallaVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Niveles", x => x.Id);
                    table.CheckConstraint("CK_Niveles_Numero_Positivo", "[Numero] > 0");
                    table.ForeignKey(
                        name: "FK_Niveles_Mallas_MallaVersionId",
                        column: x => x.MallaVersionId,
                        principalTable: "Mallas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Asignaturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MallaVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NivelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Creditos = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Horas = table.Column<int>(type: "int", nullable: false),
                    UnidadOrganizacional = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asignaturas", x => x.Id);
                    table.CheckConstraint("CK_Asignaturas_Creditos_Positivos", "[Creditos] > 0");
                    table.ForeignKey(
                        name: "FK_Asignaturas_Mallas_MallaVersionId",
                        column: x => x.MallaVersionId,
                        principalTable: "Mallas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Asignaturas_Niveles_NivelId",
                        column: x => x.NivelId,
                        principalTable: "Niveles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asignaturas_MallaVersionId_Codigo",
                table: "Asignaturas",
                columns: new[] { "MallaVersionId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asignaturas_NivelId",
                table: "Asignaturas",
                column: "NivelId");

            migrationBuilder.CreateIndex(
                name: "IX_Carreras_Codigo",
                table: "Carreras",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mallas_CarreraId_Version",
                table: "Mallas",
                columns: new[] { "CarreraId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Niveles_MallaVersionId_Numero",
                table: "Niveles",
                columns: new[] { "MallaVersionId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Asignaturas");

            migrationBuilder.DropTable(
                name: "Niveles");

            migrationBuilder.DropTable(
                name: "Mallas");

            migrationBuilder.DropTable(
                name: "Carreras");
        }
    }
}
