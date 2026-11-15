using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
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
    string Evidencia);

public interface IComparadorContenidosIa
{
    Task<IReadOnlyList<CoincidenciaIa>> CompararAsync(
        IReadOnlyList<ContenidoOrigenIa> origen,
        IReadOnlyList<ContenidoDestinoIa> destino,
        CancellationToken cancellationToken = default);
}

public sealed class OllamaComparadorContenidos(
    HttpClient httpClient,
    IConfiguration configuration,
    IMemoryCache cache) : IComparadorContenidosIa
{
    private const string ModeloPredeterminado = "bge-m3";

    public async Task<IReadOnlyList<CoincidenciaIa>> CompararAsync(
        IReadOnlyList<ContenidoOrigenIa> origen,
        IReadOnlyList<ContenidoDestinoIa> destino,
        CancellationToken cancellationToken = default)
    {
        if (origen.Count == 0 || destino.Count == 0)
            return Array.Empty<CoincidenciaIa>();

        var modelo = configuration["Ollama:EmbeddingModel"]
            ?? ModeloPredeterminado;
        var textos = origen.Select(x => x.Descripcion.Trim())
            .Concat(destino.Select(x => x.Descripcion.Trim()))
            .ToArray();
        var datos = JsonSerializer.Serialize(new { modelo, textos });
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(datos)));
        var cacheKey = $"ollama:embeddings:{hash}";

        if (!cache.TryGetValue(cacheKey, out float[][]? embeddings)
            || embeddings is null)
        {
            using var respuesta = await httpClient.PostAsJsonAsync(
                "api/embed",
                new { model = modelo, input = textos },
                cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                var estado = respuesta.StatusCode;
                throw new HttpRequestException(
                    $"Ollama respondiÃ³ con HTTP {(int)estado}.",
                    inner: null,
                    statusCode: estado);
            }

            var documento = await respuesta.Content
                .ReadFromJsonAsync<RespuestaEmbeddings>(
                    cancellationToken: cancellationToken);
            embeddings = documento?.Embeddings;
            if (embeddings is null || embeddings.Length != textos.Length
                || embeddings.Any(x => x.Length == 0))
            {
                throw new InvalidOperationException(
                    "Ollama no devolviÃ³ los embeddings esperados. Verifica el modelo de embeddings.");
            }

            cache.Set(cacheKey, embeddings, TimeSpan.FromHours(12));
        }

        return CrearCoincidencias(origen, destino, embeddings);
    }

    private static IReadOnlyList<CoincidenciaIa> CrearCoincidencias(
        IReadOnlyList<ContenidoOrigenIa> origen,
        IReadOnlyList<ContenidoDestinoIa> destino,
        float[][] embeddings)
    {
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
                $"Mayor similitud con {origen[mejorIndice].Codigo}: {origen[mejorIndice].Descripcion}"));
        }
        return resultados;
    }

    private static double Coseno(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new InvalidOperationException("Ollama devolviÃ³ embeddings incompatibles.");

        double producto = 0, normaA = 0, normaB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            producto += a[i] * b[i];
            normaA += a[i] * a[i];
            normaB += b[i] * b[i];
        }

        if (normaA == 0 || normaB == 0)
            return 0;
        return producto / (Math.Sqrt(normaA) * Math.Sqrt(normaB));
    }

    private sealed record RespuestaEmbeddings(
        [property: JsonPropertyName("embeddings")] float[][] Embeddings);
}