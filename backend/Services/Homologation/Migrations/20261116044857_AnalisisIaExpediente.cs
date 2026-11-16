using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologationService.Migrations
{
    /// <inheritdoc />
    public partial class AnalisisIaExpediente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Expedientes_UnoActivoPorEstudianteYCarrera",
                table: "Expedientes");

            migrationBuilder.AddColumn<bool>(
                name: "EsAnalisisIa",
                table: "EjecucionesReglas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "UX_Expedientes_UnoActivoPorEstudianteYCarrera",
                table: "Expedientes",
                columns: new[] { "EstudianteId", "CarreraDestinoId" },
                unique: true,
                filter: "[Estado] IN ('Borrador','EnRevision','AnalizadoConIa')");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesReglas_ExpedienteId",
                table: "EjecucionesReglas",
                column: "ExpedienteId",
                unique: true,
                filter: "[EsAnalisisIa] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Expedientes] SET [Estado] = 'EnRevision' WHERE [Estado] = 'AnalizadoConIa';");

            migrationBuilder.DropIndex(
                name: "UX_Expedientes_UnoActivoPorEstudianteYCarrera",
                table: "Expedientes");

            migrationBuilder.DropIndex(
                name: "IX_EjecucionesReglas_ExpedienteId",
                table: "EjecucionesReglas");

            migrationBuilder.DropColumn(
                name: "EsAnalisisIa",
                table: "EjecucionesReglas");

            migrationBuilder.CreateIndex(
                name: "UX_Expedientes_UnoActivoPorEstudianteYCarrera",
                table: "Expedientes",
                columns: new[] { "EstudianteId", "CarreraDestinoId" },
                unique: true,
                filter: "[Estado] IN ('Borrador','EnRevision')");
        }
    }
}
