using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace DocumentsService.Tests;

public sealed class SwaggerTests : IClassFixture<DocumentsApiFactory>
{
    private readonly DocumentsApiFactory factory;
    public SwaggerTests(DocumentsApiFactory factory) => this.factory = factory;

    [Fact]
    public void Swagger_genera_los_endpoints_de_Xlsx_y_Pdf_con_archivo_multipart()
    {
        using var scope = factory.Services.CreateScope();
        var swagger = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        foreach (var path in new[] { "/api/v1/documentos/importaciones", "/api/v1/documentos/importaciones/confirmar",
            "/api/v1/documentos/records/analizar-registro", "/api/v1/documentos/records/registrar" })
        {
            var operation = swagger.Paths[path].Operations[Microsoft.OpenApi.Models.OperationType.Post];
            Assert.Contains("multipart/form-data", operation.RequestBody.Content.Keys);
        }
    }
}
