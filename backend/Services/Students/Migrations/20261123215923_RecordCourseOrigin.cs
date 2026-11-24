using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentsService.Migrations
{
    /// <inheritdoc />
    public partial class RecordCourseOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarreraOrigenNombre",
                table: "AsignaturasCursadas",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarreraOrigenNombre",
                table: "AsignaturasCursadas");
        }
    }
}
