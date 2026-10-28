using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentsService.Migrations
{
    /// <inheritdoc />
    public partial class IdentificacionYHoras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Estudiantes_Cedula_Formato",
                table: "Estudiantes");

            migrationBuilder.AlterColumn<string>(
                name: "Cedula",
                table: "Estudiantes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<string>(
                name: "TipoIdentificacion",
                table: "Estudiantes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cedula");

            migrationBuilder.AddColumn<int>(
                name: "Horas",
                table: "AsignaturasCursadas",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Estudiantes_Cedula_Formato",
                table: "Estudiantes",
                sql: "[TipoIdentificacion] <> 'Cedula' OR (LEN([Cedula]) = 10 AND [Cedula] NOT LIKE '%[^0-9]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Estudiantes_Pasaporte_Formato",
                table: "Estudiantes",
                sql: "[TipoIdentificacion] <> 'Pasaporte' OR (LEN([Cedula]) BETWEEN 5 AND 20 AND [Cedula] NOT LIKE '%[^A-Za-z0-9]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AsignaturasCursadas_Horas_Positivas",
                table: "AsignaturasCursadas",
                sql: "[Horas] IS NULL OR [Horas] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Estudiantes_Cedula_Formato",
                table: "Estudiantes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Estudiantes_Pasaporte_Formato",
                table: "Estudiantes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AsignaturasCursadas_Horas_Positivas",
                table: "AsignaturasCursadas");

            migrationBuilder.DropColumn(
                name: "TipoIdentificacion",
                table: "Estudiantes");

            migrationBuilder.DropColumn(
                name: "Horas",
                table: "AsignaturasCursadas");

            migrationBuilder.AlterColumn<string>(
                name: "Cedula",
                table: "Estudiantes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Estudiantes_Cedula_Formato",
                table: "Estudiantes",
                sql: "LEN([Cedula]) = 10 AND [Cedula] NOT LIKE '%[^0-9]%'");
        }
    }
}
