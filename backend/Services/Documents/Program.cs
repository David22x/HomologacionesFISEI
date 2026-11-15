using BuildingBlocks.Exceptions;
using BuildingBlocks.Swagger;
using BuildingBlocks.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Servicios base comunes
builder.Services.AddControllers();
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddCustomSwagger("Documents Service API");
builder.Services.AddHttpClient("StudentsService", client =>
{
    var baseAddress = builder.Configuration["StudentsService:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseAddress))
        throw new InvalidOperationException("Configura StudentsService:BaseUrl para contactar a Students Service.");
    client.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
});

var app = builder.Build();

// Manejo global de excepciones estandarizado
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
