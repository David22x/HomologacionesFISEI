using BuildingBlocks.Resilience;
using BuildingBlocks.Authentication;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Swagger;
using Microsoft.EntityFrameworkCore;
using ReportsService.Data;
using ReportsService.Services;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("ReportsConnection")
    ?? throw new InvalidOperationException("Configura ConnectionStrings:ReportsConnection.");
builder.Services.AddDbContext<ReportsDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddCustomSwagger("Reports Service API");
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddHttpClient("Homologation", client =>
    client.BaseAddress = new Uri(builder.Configuration["HomologationService:BaseUrl"] ?? "http://localhost:5196"));
builder.Services.AddHttpClient("Students", client =>
    client.BaseAddress = new Uri(builder.Configuration["StudentsService:BaseUrl"] ?? "http://localhost:5194"));
builder.Services.AddHttpClient("Academic", client =>
    client.BaseAddress = new Uri(builder.Configuration["AcademicService:BaseUrl"] ?? "http://localhost:5193"));
builder.Services.AddScoped<FuenteInformeClient>();
// Uso académico del proyecto.
QuestPDF.Settings.License = LicenseType.Community;
builder.Services.AddServiceResilience();
builder.Services.AddReadinessCheck("database", async (services, ct) => await services.GetRequiredService<ReportsDbContext>().Database.CanConnectAsync(ct));
var app = builder.Build();
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue("Reports:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ReportsDbContext>().Database.MigrateAsync();
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
app.MapServiceHealth();
app.MapControllers();
app.Run();
public partial class Program { }
