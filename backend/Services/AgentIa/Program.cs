using BuildingBlocks.Resilience;
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

builder.Services.AddServiceResilience();
builder.Services.AddHttpClient("OllamaHealth", client => { client.BaseAddress = new Uri(ollamaBaseUrl); client.Timeout = TimeSpan.FromSeconds(2); });
builder.Services.AddReadinessCheck("ollama", async (services, ct) => {
    using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("OllamaHealth");
    using var response = await client.GetAsync("api/tags", ct);
    if (!response.IsSuccessStatusCode) return false;
    var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct);
    var model = builder.Configuration["Ollama:EmbeddingModel"] ?? "bge-m3";
    var expected = model.Contains(':') ? model : model + ":latest";
    return json.GetProperty("models").EnumerateArray().Any(x => x.GetProperty("name").GetString() == expected || x.GetProperty("name").GetString() == model);
});
var app = builder.Build();
if (parametrosReglas.NotaMinima is null)
    app.Logger.LogWarning("No hay nota mínima configurada. El motor utiliza la aprobación declarada en el historial; no se asume un mínimo académico.");
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapServiceHealth();
app.Run();
