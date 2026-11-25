using BuildingBlocks.Authentication;
using BuildingBlocks.Exceptions;
using HomologationService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"]
    ?? "http://localhost:11434/";
builder.Services.AddHttpClient<IComparadorContenidosIa, OllamaComparadorContenidos>(client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("Ollama:TimeoutSeconds", 120));
});
builder.Services.AddHttpClient<ExtractorRecordIa>(client => { client.BaseAddress = new Uri(ollamaBaseUrl); client.Timeout = TimeSpan.FromSeconds(180); });
var parametrosReglas = ParametrosReglas.DesdeConfiguracion(builder.Configuration);
builder.Services.AddSingleton(parametrosReglas);
builder.Services.AddScoped<IMotorReglasHomologacion, MotorReglasHomologacion>();
builder.Services.AddScoped<IAgenteIaService, AgenteIaService>();

var app = builder.Build();
if (parametrosReglas.NotaMinima is null)
    app.Logger.LogWarning("No hay nota mínima configurada. El motor utiliza la aprobación declarada en el historial; no se asume un mínimo académico.");
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
