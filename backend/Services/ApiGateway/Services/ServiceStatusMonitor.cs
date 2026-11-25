using System.Net;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.Services;

public sealed class ServiceStatusMonitor(IHttpClientFactory clients, IConfiguration config)
{
    public sealed record ServiceStatus(string Service, bool Available);
    public async Task<ServiceStatus[]> CheckAsync(CancellationToken ct)
    {
        var specs = new[] { ("Auth", "/auth", 5192), ("Academic", "/api/v1/catalogo", 5193),
            ("Students", "/api/v1/estudiantes", 5194), ("Homologation", "/api/v1/homologacion", 5196),
            ("Documents", "/api/v1/documentos", 5104), ("Reports", "/api/v1/informes", 5198), ("AgentIA", "/api/v1/agent-ia", 5197) };
        var tasks = specs.Select(async spec => {
            var route = config.GetSection("Routes").GetChildren().FirstOrDefault(r =>
                (r["DownstreamPathTemplate"] ?? "").StartsWith(spec.Item2, StringComparison.OrdinalIgnoreCase));
            var host = route?["DownstreamHostAndPorts:0:Host"] ?? "localhost";
            var port = route?["DownstreamHostAndPorts:0:Port"] ?? spec.Item3.ToString();
            var scheme = route?["DownstreamScheme"] ?? "http";
            var uri = config[$"Monitoring:{spec.Item1}Url"] ?? $"{scheme}://{host}:{port}/health/ready";
            return new ServiceStatus(spec.Item1, await Check(uri, ct));
        });
        var result = await Task.WhenAll(tasks);
        return result.Append(new("Gateway", true)).ToArray();
    }
    private async Task<bool> Check(string uri, CancellationToken ct)
    {
        try {
            using var client = clients.CreateClient("ServiceMonitor");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return false; }
    }
}
