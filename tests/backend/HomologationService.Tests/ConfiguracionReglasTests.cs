using System.Text.Json;
using HomologationService.Services;
using Microsoft.Extensions.Configuration;

namespace HomologationService.Tests;

public sealed class ConfiguracionReglasTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_criterio_configurado_no_se_inventa_una_nota_minima(string? value)
    {
        var parametros = ParametrosReglas.DesdeConfiguracion(Config(value));
        Assert.Null(parametros.NotaMinima);
        Assert.Equal(ResultadoHomologacion.Homologable, new MotorReglasHomologacion(parametros).Evaluar(Entrada(6m)).Resultado);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("10.1")]
    [InlineData("texto")]
    public void Rechaza_configuracion_fuera_de_escala_o_no_numerica(string value)
        => Assert.Throws<InvalidOperationException>(() => ParametrosReglas.DesdeConfiguracion(Config(value)));

    [Theory]
    [InlineData(6.99, ResultadoHomologacion.OrigenNoAprobado)]
    [InlineData(7, ResultadoHomologacion.Homologable)]
    public void Nota_minima_configurada_se_aplica_inclusivamente_y_se_guarda_con_el_resultado(double nota, ResultadoHomologacion expected)
    {
        var parametros = ParametrosReglas.DesdeConfiguracion(Config("7"));
        var result = new MotorReglasHomologacion(parametros).Evaluar(Entrada((decimal)nota));
        Assert.Equal(expected, result.Resultado);
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(7m, json.GetProperty("parametrosAplicados").GetProperty("notaMinima").GetDecimal());
        Assert.Equal(.8m, json.GetProperty("parametrosAplicados").GetProperty("razonCreditosMinima").GetDecimal());
    }

    private static IConfiguration Config(string? value) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Homologacion:Reglas:NotaMinima"] = value }).Build();
    private static EvaluacionHomologacion Entrada(decimal nota) => new(3, 3, 48, 48, true, nota, false, false,
        [new ContenidoDestino("tema", 48)], [new CoincidenciaContenido("tema", 1)]);
}
