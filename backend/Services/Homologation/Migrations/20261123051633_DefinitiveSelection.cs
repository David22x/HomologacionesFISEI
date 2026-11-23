using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologationService.Migrations
{
    /// <inheritdoc />
    public partial class DefinitiveSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeleccionJson",
                table: "Expedientes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeleccionJson",
                table: "Expedientes");
        }
    }
}
