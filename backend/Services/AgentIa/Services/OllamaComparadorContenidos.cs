using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace HomologationService.Services;

public sealed record ContenidoOrigenIa(string Codigo, string Descripcion);

public sealed record ContenidoDestinoIa(
    string Codigo,
    string Descripcion,
    decimal? Horas = null);

public sealed record CoincidenciaIa(
    string CodigoDestino,
    decimal Similitud,
    string Evidencia,
    string? CodigoOrigen = null);

public interface IComparadorContenidosIa
{
    Task<IReadOnlyList<CoincidenciaIa>> CompararAsync(
        IReadOnlyList<ContenidoOrigenIa> origen,
        IReadOnlyList<ContenidoDestinoIa> destino,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Genera embeddings locales con Ollama. La cache guarda cada contenido del
/// catalogo por separado, de modo que se reutiliza aunque cambie el expediente.
/// </summary>
public sealed class OllamaComparadorContenidos(
    HttpClient httpClient,
    IConfiguration configuration,
    IMemoryCache cache) : IComparadorContenidosIa
{
    private const string ModeloPredeterminado = "bge-m3";
    private const int IntentosMaximos = 3;
    private static readonly TimeSpan DuracionCache = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<CoincidenciaIa>> CompararAsync(
        IReadOnlyList<ContenidoOrigenIa> origen,
        IReadOnlyList<ContenidoDestinoIa> destino,
        CancellationToken cancellationToken = default)
    {
        if (origen.Count == 0 || destino.Count == 0)
            return Array.Empty<CoincidenciaIa>();

        var modelo = configuration["Ollama:EmbeddingModel"]
            ?? ModeloPredeterminado;

        // Los códigos enlazan las evidencias, pero no forman parte del contenido académico.
        var textos = origen
            .Select(x => NormalizarTexto(x.Descripcion))
            .Concat(destino.Select(x => NormalizarTexto(x.Descripcion)))
            .ToArray();
        var embeddings = await ObtenerEmbeddingsAsync(modelo, textos, cancellationToken);

        var resultados = new List<CoincidenciaIa>(destino.Count);
        for (var indiceDestino = 0; indiceDestino < destino.Count; indiceDestino++)
        {
            var mejorIndice = 0;
            var mejorSimilitud = -1d;
            for (var indiceOrigen = 0; indiceOrigen < origen.Count; indiceOrigen++)
            {
                var similitud = Coseno(
                    embeddings[indiceOrigen],
                    embeddings[origen.Count + indiceDestino]);
                if (similitud > mejorSimilitud)
                {
                    mejorIndice = indiceOrigen;
                    mejorSimilitud = similitud;
                }
            }

            resultados.Add(new CoincidenciaIa(
                destino[indiceDestino].Codigo,
                (decimal)Math.Clamp(mejorSimilitud, 0d, 1d),
                $"Mayor similitud con {origen[mejorIndice].Codigo}: {origen[mejorIndice].Descripcion}",
                origen[mejorIndice].Codigo));
        }

        return resultados;
    }

    private async Task<float[][]> ObtenerEmbeddingsAsync(
        string modelo,
        string[] textos,
        CancellationToken cancellationToken)
    {
        var resultado = new float[textos.Length][];
        var indicesFaltantes = new List<int>();
        for (var i = 0; i < textos.Length; i++)
        {
            var clave = CrearClaveCache(modelo, textos[i]);
            if (cache.TryGetValue(clave, out float[]? embedding) && embedding is { Length: > 0 })
                resultado[i] = embedding;
            else
                indicesFaltantes.Add(i);
        }

        if (indicesFaltantes.Count > 0)
        {
            var textosFaltantes = indicesFaltantes.Select(i => textos[i]).ToArray();
            var nuevos = await SolicitarEmbeddingsConReintentosAsync(
                modelo, textosFaltantes, cancellationToken);

            for (var i = 0; i < indicesFaltantes.Count; i++)
            {
                var indice = indicesFaltantes[i];
                resultado[indice] = nuevos[i];
                cache.Set(
                    CrearClaveCache(modelo, textos[indice]),
                    nuevos[i],
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = DuracionCache });
            }
        }

        return resultado;
    }

    private async Task<float[][]> SolicitarEmbeddingsConReintentosAsync(
        string modelo,
        string[] textos,
        CancellationToken cancellationToken)
    {
        using var presupuesto = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        presupuesto.CancelAfter(TimeSpan.FromSeconds(
            configuration.GetValue("Ollama:OperationTimeoutSeconds", 60)));
        var token = presupuesto.Token;
        for (var intento = 1; ; intento++)
        {
            try
            {
                using var respuesta = await httpClient.PostAsJsonAsync(
                    "api/embed",
                    new { model = modelo, input = textos },
                    token);

                if (EsReintentable(respuesta.StatusCode) && intento < IntentosMaximos)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, intento - 1)), token);
                    continue;
                }

                if (!respuesta.IsSuccessStatusCode)
                    throw new HttpRequestException(
                        $"Ollama respondio con HTTP {(int)respuesta.StatusCode}.",
                        inner: null,
                        statusCode: respuesta.StatusCode);

                var documento = await respuesta.Content.ReadFromJsonAsync<RespuestaEmbeddings>(
                    cancellationToken: token);
                var embeddings = documento?.Embeddings;
                if (embeddings is null || embeddings.Length != textos.Length
                    || embeddings.Any(x => x is null || x.Length == 0
                        || x.Length != embeddings[0].Length
                        || x.Any(v => !float.IsFinite(v))
                        || !x.Any(v => v != 0)))
                    throw new InvalidOperationException(
                        "Ollama no devolvio todos los embeddings esperados. Verifica que el modelo bge-m3 este descargado.");

                return embeddings;
            }
            catch (HttpRequestException ex) when (
                !token.IsCancellationRequested && intento < IntentosMaximos
                && (ex.StatusCode is null || EsReintentable(ex.StatusCode.Value)))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, intento - 1)), token);
            }
            catch (TaskCanceledException) when (
                !token.IsCancellationRequested && intento < IntentosMaximos)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, intento - 1)), token);
            }
        }
    }

    private static bool EsReintentable(HttpStatusCode estado) =>
        estado == HttpStatusCode.RequestTimeout
        || (int)estado == 429
        || (int)estado >= 500;

    private static string NormalizarTexto(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                limpio.Append(char.ToLowerInvariant(caracter));
        }

        return string.Join(' ', limpio.ToString()
            .Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string CrearClaveCache(string modelo, string texto)
    {
        var contenido = Encoding.UTF8.GetBytes($"{modelo}\n{texto}");
        return $"ollama:catalogo:{Convert.ToHexString(SHA256.HashData(contenido))}";
    }

    private static double Coseno(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new InvalidOperationException("Ollama devolvio embeddings incompatibles.");

        double producto = 0, normaA = 0, normaB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            producto += (double)a[i] * b[i];
            normaA += (double)a[i] * a[i];
            normaB += (double)b[i] * b[i];
        }

        if (normaA == 0 || normaB == 0)
            return 0;
        return producto / (Math.Sqrt(normaA) * Math.Sqrt(normaB));
    }

    private sealed record RespuestaEmbeddings(
        [property: JsonPropertyName("embeddings")] float[][] Embeddings);
}
