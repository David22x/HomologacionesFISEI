using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologationService.Migrations
{
    /// <inheritdoc />
    public partial class PrecedentesDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrigenCatalogoIdsJson",
                table: "Decisiones",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrigenCatalogoIdsJson",
                table: "Decisiones");
        }
    }
}
