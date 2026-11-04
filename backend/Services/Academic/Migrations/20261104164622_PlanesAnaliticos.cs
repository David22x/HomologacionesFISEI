using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcademicService.Migrations
{
    /// <inheritdoc />
    public partial class PlanesAnaliticos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanesAnaliticos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Contenido = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SubidoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubidoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesAnaliticos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanesAnaliticos_Asignaturas_AsignaturaId",
                        column: x => x.AsignaturaId,
                        principalTable: "Asignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAnaliticos_AsignaturaId",
                table: "PlanesAnaliticos",
                column: "AsignaturaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanesAnaliticos");
        }
    }
}
