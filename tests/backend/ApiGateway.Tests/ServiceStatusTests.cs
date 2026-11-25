using System.Net;
using ApiGateway.Services;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.Tests;

public sealed class ServiceStatusTests
{
    [Fact]
    public async Task Servicio_caido_no_impide_consultar_el_estado_de_los_demas()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Monitoring:StudentsUrl"] = "http://students/health/ready", ["Monitoring:AuthUrl"] = "http://auth/health/ready"
        }).Build();
        using var source = new Source(); var states = await new ServiceStatusMonitor(source, config).CheckAsync(default);
        Assert.False(Assert.Single(states, s => s.Service == "Students").Available);
        Assert.True(Assert.Single(states, s => s.Service == "Auth").Available);
        Assert.True(Assert.Single(states, s => s.Service == "Gateway").Available);
        Assert.Equal(8, states.Length);
    }
    private sealed class Source : HttpMessageHandler, IHttpClientFactory {
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(request.RequestUri!.Host == "students" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
    }
}
