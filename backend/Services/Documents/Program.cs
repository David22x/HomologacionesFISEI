using BuildingBlocks.Authentication;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Servicios base comunes
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddCustomSwagger("Documents Service API");

// Configurar JWT (descomentar una vez que el Gateway valide roles, si es necesario)
// builder.Services.AddCustomJwtAuthentication(builder.Configuration);
// builder.Services.AddAuthorization();

var app = builder.Build();

// Manejo global de excepciones estandarizado
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// app.UseAuthentication();
// app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();