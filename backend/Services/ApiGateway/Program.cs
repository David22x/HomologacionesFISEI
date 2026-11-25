using ApiGateway.Services;
using BuildingBlocks.Resilience;
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
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .AllowAnyHeader();
    });
});
builder.Services.AddCustomJwtAuthentication(builder.Configuration);
builder.Services.AddHealthChecks();
var ocelotConfigurationPath = builder.Configuration["Ocelot:ConfigurationFile"]
    ?? Path.Combine(AppContext.BaseDirectory, "ocelot.json");
builder.Configuration.AddJsonFile(ocelotConfigurationPath, optional: false, reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration);

builder.Services.AddHttpClient("ServiceMonitor", client => client.Timeout = TimeSpan.FromSeconds(3));
builder.Services.AddSingleton<ServiceStatusMonitor>();
builder.Services.AddServiceResilience();
var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseHealthChecks("/health");
app.UseHealthChecks("/health/ready");
app.UseCors("Frontend");
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments(
        "/internal",
        StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseAuthentication();
app.Use(async (context, next) => {
    if (context.Request.Path == "/status/services" && HttpMethods.IsGet(context.Request.Method)) {
        if (context.User.Identity?.IsAuthenticated != true) { context.Response.StatusCode = 401; return; }
        var services = await context.RequestServices.GetRequiredService<ServiceStatusMonitor>().CheckAsync(context.RequestAborted);
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new { services });
        return;
    }
    await next();
});
await app.UseOcelot();

app.Run();

public partial class Program { }
