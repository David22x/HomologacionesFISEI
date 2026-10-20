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

// Migraciones y semilla del catalogo (solo en Desarrollo)
await AcademicDbSeeder.SeedAsync(app.Services, app.Environment.IsDevelopment());

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
