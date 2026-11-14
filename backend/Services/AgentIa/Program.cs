using BuildingBlocks.Authentication;
using BuildingBlocks.Exceptions;
using HomologationService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IComparadorContenidosIa, GeminiComparadorContenidos>();
builder.Services.AddSingleton(new ParametrosReglas());
builder.Services.AddScoped<IMotorReglasHomologacion, MotorReglasHomologacion>();
builder.Services.AddScoped<IAgenteIaService, AgenteIaService>();

var app = builder.Build();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
