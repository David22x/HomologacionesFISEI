using AcademicService.Data;
using BuildingBlocks.Authentication;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Swagger;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AcademicConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configura ConnectionStrings:AcademicConnection mediante User Secrets o una variable de entorno.");
}

builder.Services.AddDbContext<AcademicDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddCustomSwagger("Academic Service API - Catalogo academico");
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

// Migraciones y semilla del catalogo (solo en Desarrollo). La opcion permite
// probar el host real con una base aislada sin aplicar migraciones de SQL Server.
var seedOnStartup = app.Environment.IsDevelopment()
    && builder.Configuration.GetValue("Academic:SeedOnStartup", true);
await AcademicDbSeeder.SeedAsync(app.Services, seedOnStartup);
await AcademicTemasSeeder.SeedAsync(app.Services, seedOnStartup);

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program { }
