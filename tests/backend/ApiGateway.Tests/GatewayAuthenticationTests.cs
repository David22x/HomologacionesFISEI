using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Tests;

public sealed class GatewayAuthenticationTests : IClassFixture<GatewayApiFactory>
{
    private readonly HttpClient _client;

    public GatewayAuthenticationTests(GatewayApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RutaProtegida_SinToken_Devuelve401()
    {
        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RutaProtegida_ConTokenVencido_Devuelve401()
    {
        var token = GatewayApiFactory.CreateToken(DateTime.UtcNow.AddMinutes(-1));

        var response = await GetWithTokenAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RutaProtegida_ConTokenAlterado_Devuelve401()
    {
        var token = GatewayApiFactory.CreateToken(DateTime.UtcNow.AddMinutes(5));
        var tokenParts = token.Split('.');
        var signature = tokenParts[2];
        tokenParts[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        var alteredToken = string.Join('.', tokenParts);

        var response = await GetWithTokenAsync(alteredToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/internal/health")]
    [InlineData("/INTERNAL/health")]
    public async Task RutaInternal_NoEstaExpuestaPorElGateway(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> GetWithTokenAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}

public sealed class GatewayApiFactory : WebApplicationFactory<Program>
{
    private static readonly string SecretKey = Convert.ToHexString(
        RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:SecretKey", SecretKey);
    }

    public static string CreateToken(DateTime expiresAt)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var signingCredentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HomologacionesFISEI",
            audience: "HomologacionesFISEIClient",
            claims: [new Claim(ClaimTypes.NameIdentifier, "test-user")],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: expiresAt,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
