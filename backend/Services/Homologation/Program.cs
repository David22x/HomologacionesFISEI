using BuildingBlocks.Authentication;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Swagger;
using HomologationService.Data;
using Microsoft.EntityFrameworkCore;
using HomologationService.Services;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("HomologationConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configura ConnectionStrings:HomologationConnection mediante User Secrets o una variable de entorno.");
}

builder.Services.AddDbContext<HomologationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddHttpClient("StudentsService", client =>
{
    var baseUrl = builder.Configuration["StudentsService:BaseUrl"] ?? "http://localhost:5194";
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
});
builder.Services.AddHttpClient("AcademicService", client =>
{
    var baseUrl = builder.Configuration["AcademicService:BaseUrl"] ?? "http://localhost:5193";
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddCustomSwagger("Homologation Service API - Homologacion de materias y creditos");
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();


builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<
    IComparadorContenidosIa,
    GeminiComparadorContenidos>();

builder.Services.AddSingleton(new ParametrosReglas());
builder.Services.AddScoped<
    IMotorReglasHomologacion,
    MotorReglasHomologacion>();

builder.Services.AddScoped<
    IAgenteIaService,
    AgenteIaService>();

var app = builder.Build();

// Migraciones automaticas solo en Desarrollo. La opcion permite probar el host
// con una base aislada sin aplicar migraciones de SQL Server.
var migrateOnStartup = app.Environment.IsDevelopment()
    && builder.Configuration.GetValue("Homologation:MigrateOnStartup", true);
if (migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HomologationDbContext>();
    await db.Database.MigrateAsync();
}

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
