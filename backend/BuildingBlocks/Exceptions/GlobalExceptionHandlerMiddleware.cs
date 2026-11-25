using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Exceptions;

public class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente ya cerró la conexión: no escribir otra respuesta ni registrar un error 500.
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) { logger.LogWarning(ex, "Respuesta interrumpida {TraceId}", context.TraceIdentifier); throw; }
            var database = FindDatabaseError(ex);
            var status = ex switch {
                OperationCanceledException => StatusCodes.Status504GatewayTimeout,
                HttpRequestException { StatusCode: HttpStatusCode.GatewayTimeout } => StatusCodes.Status504GatewayTimeout,
                HttpRequestException => StatusCodes.Status503ServiceUnavailable,
                _ when database is not null => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };
            if (status == 500) logger.LogError(ex, "Error no controlado {TraceId}", context.TraceIdentifier);
            else logger.LogWarning(ex, "Dependencia no disponible {TraceId}", context.TraceIdentifier);
            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            if (status is 503 or 504) context.Response.Headers.RetryAfter = "15";
            var detail = status switch {
                503 => "Un servicio necesario no está disponible temporalmente. Intenta nuevamente en unos segundos.",
                504 => "Un servicio necesario tardó demasiado en responder. Intenta nuevamente en unos segundos.",
                _ => "Ha ocurrido un error inesperado al procesar la solicitud."
            };
            if (status is 503 or 504 && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
                detail += " No se pudo confirmar el resultado de la operación; consulta su estado antes de repetirla.";
            var problem = new ProblemDetails { Status = status, Title = status == 503 ? "Servicio temporalmente no disponible" : status == 504 ? "Tiempo de espera agotado" : "Error interno del servidor", Detail = detail, Instance = context.Request.Path };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }
    private static DbException? FindDatabaseError(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is DbException db) {
                if (db.IsTransient) return db;
                var number = db.GetType().GetProperty("Number")?.GetValue(db) as int?;
                if (number is -2 or -1 or 2 or 5 or 20 or 40 or 53 or 64 or 121 or 233 or 258 or 1205 or 4060 or 18456 or 10060 or 10061 or 10928 or 10929 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920) return db;
            }
        return null;
    }
}
