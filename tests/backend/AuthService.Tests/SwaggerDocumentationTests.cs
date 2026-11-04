using System.Net;
using System.Text.Json;

namespace AuthService.Tests;

public sealed class SwaggerDocumentationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task SwaggerJson_DocumentaLosEndpointsDeAuthYElEsquemaBearer()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        var login = paths.GetProperty("/auth/login").GetProperty("post").GetProperty("responses");
        foreach (var codigo in new[] { "200", "400", "401" })
        {
            Assert.True(login.TryGetProperty(codigo, out _), $"POST /auth/login does not document {codigo}.");
        }

        var me = paths.GetProperty("/auth/me").GetProperty("get").GetProperty("responses");
        foreach (var codigo in new[] { "200", "401" })
        {
            Assert.True(me.TryGetProperty(codigo, out _), $"GET /auth/me does not document {codigo}.");
        }

        Assert.True(
            root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _),
            "The Bearer security scheme is not documented.");
    }
}
