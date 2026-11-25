using BuildingBlocks.Resilience;
using BuildingBlocks.Authentication;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Servicios base comunes
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddCustomSwagger("Plantilla de Microservicio FISEI");
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

builder.Services.AddServiceResilience();
var app = builder.Build();

// Manejo global de excepciones estandarizado (RFC 7807 ProblemDetails)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Endpoint de diagnóstico y salud
app.MapServiceHealth();
app.MapControllers();

app.Run();