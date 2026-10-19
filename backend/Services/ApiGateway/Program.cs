using BuildingBlocks.Exceptions;
using BuildingBlocks.Authentication;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .WithMethods("GET", "POST", "OPTIONS")
            .AllowAnyHeader();
    });
});
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "ocelot.json"),
    optional: false,
    reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors("Frontend");
app.UseAuthentication();
await app.UseOcelot();

app.Run();
