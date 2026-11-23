using System.Net.Http.Headers;
using System.Text.Json;
using HomologationService.Models;
using static HomologationService.Services.DatosEvaluacionClient;

namespace HomologationService.Services;

internal sealed class GeneradorPropuestas(DatosEvaluacionClient datos)
{
    internal async Task<Propuesta?> GenerarGrupoAsync(Expediente expediente, Guid destinoId,
        IReadOnlyCollection<Guid> origenIds, AuthenticationHeaderValue bearer, CancellationToken ct)
    {
        if (origenIds.Count < 2 || origenIds.Any(id => id == Guid.Empty)
            || origenIds.Distinct().Count() != origenIds.Count)
            return null;

        var historial = await datos.HistorialAsync(expediente.EstudianteId, bearer, ct);
        var origenesPorId = historial?.Asignaturas.ToDictionary(x => x.Id) ?? [];
        if (origenIds.Any(id => !origenesPorId.ContainsKey(id)))
            return null;

        var destinos = await datos.DestinosAsync(expediente.MallaDestinoId, bearer, ct);
        var destino = destinos.SingleOrDefault(x => x.Id == destinoId);
        if (destino is null)
            return null;

        var origenes = origenIds.Select(id => origenesPorId[id]).ToArray();
        if (origenes.Where(x => x.AsignaturaId.HasValue).Select(x => x.AsignaturaId)
            .Distinct().Count() != origenes.Count(x => x.AsignaturaId.HasValue))
            return null;

        var grupos = new List<(Origen Origen, decimal? Creditos, int? Horas, bool Transversal, Contenido[] Contenidos)>();
        foreach (var origen in origenes)
        {
            Asignatura? ficha = null;
            Contenido[] temas = [];
            if (origen.AsignaturaId is Guid asignaturaId)
            {
                ficha = await datos.AsignaturaAsync(asignaturaId, bearer, ct);
                if (ficha is not null)
                    temas = await datos.ContenidosAsync(asignaturaId, bearer, ct);
            }

            grupos.Add((origen, origen.Creditos ?? ficha?.Creditos, origen.Horas ?? ficha?.Horas,
                ficha?.EsTransversal ?? false, temas));
        }

        var contenidosOrigen = grupos.All(x => x.Contenidos.Length > 0)
            ? grupos.SelectMany(x => x.Contenidos).DistinctBy(x => x.Codigo).ToArray()
            : [];
        var contenidosDestino = await datos.ContenidosAsync(destino.Id, bearer, ct);
        var solicitud = new SolicitudReglas(
            grupos.All(x => x.Creditos.HasValue) ? grupos.Sum(x => x.Creditos!.Value) : null,
            destino.Creditos,
            grupos.All(x => x.Horas.HasValue) ? grupos.Sum(x => x.Horas!.Value) : null,
            destino.Horas, true, grupos.Min(x => x.Origen.Nota), false, destino.EsTransversal,
            contenidosOrigen, contenidosDestino);
        var evaluacion = await datos.EvaluarAsync(solicitud, bearer, ct);
        return new Propuesta(destino.Id, destino.Nombre,
            grupos.Select(x => new OrigenPropuesta(x.Origen.Id, x.Origen.Nombre)).ToArray(), evaluacion);
    }

    internal async Task<Analisis> GenerarAsync(Expediente expediente, AuthenticationHeaderValue bearer, CancellationToken ct)
    {
        var historial = await datos.HistorialAsync(expediente.EstudianteId, bearer, ct);
        var destinos = await datos.DestinosAsync(expediente.MallaDestinoId, bearer, ct);
        var origenes = historial?.Asignaturas ?? [];
        var propuestas = new List<Propuesta>();
        var advertencias = new List<string>();
        if (origenes.Length == 0)
            advertencias.Add("Datos incompletos: no hay asignaturas en el historial.");
        if (destinos.Length == 0)
            advertencias.Add("Datos incompletos: la malla no tiene asignaturas.");

        var contenidos = new Dictionary<Guid, Contenido[]>();
        // Cargar una vez las fichas y sílabos, para seleccionar por contenido además del nombre.
        var preparados = new List<Candidato>();
        foreach (var origen in origenes)
        {
            Asignatura? ficha = null;
            if (origen.AsignaturaId is Guid id)
            {
                ficha = await datos.AsignaturaAsync(id, bearer, ct);
            }
            var temas = ficha is not null ? await CargarContenidosAsync(ficha.Id) : [];
            preparados.Add(new Candidato(origen, origen.Creditos ?? ficha?.Creditos,
                origen.Horas ?? ficha?.Horas, ficha?.EsTransversal ?? false, temas));
        }
        foreach (var destino in destinos.OrderBy(x => x.Id))
        {
            var temasDestino = await CargarContenidosAsync(destino.Id);
            var nombreDestino = Palabras(destino.Nombre);
            var palabrasDestino = Palabras(string.Join(" ", temasDestino.Select(x => x.Descripcion)));
            var candidatos = preparados.Select(c => new
                {
                    Candidato = c,
                    Exacto = c.Origen.AsignaturaId == destino.Id || Normalizar(c.Origen.Nombre) == Normalizar(destino.Nombre),
                    Nombre = Palabras(c.Origen.Nombre).Count(nombreDestino.Contains),
                    Contenido = Palabras(string.Join(" ", c.Contenidos.Select(x => x.Descripcion))).Count(palabrasDestino.Contains)
                })
                .Where(x => x.Exacto || x.Nombre > 0 || x.Contenido >= 2)
                .OrderByDescending(x => x.Exacto).ThenByDescending(x => x.Contenido)
                .ThenByDescending(x => x.Nombre)
                .ThenBy(x => x.Candidato.Creditos is decimal c ? Math.Abs(c - destino.Creditos) : decimal.MaxValue)
                .ThenBy(x => x.Candidato.Origen.Id).Take(3).Select(x => x.Candidato).ToArray();
            var evaluadas = new List<Propuesta>();
            foreach (var candidato in candidatos) await EvaluarAsync([candidato]);

            // Una equivalencia individual suficiente evita consumir otras materias innecesariamente.
            if (!evaluadas.Any(EsViable))
            {
                for (var i = 0; i < candidatos.Length; i++)
                for (var j = i + 1; j < candidatos.Length; j++)
                {
                    var a = candidatos[i]; var b = candidatos[j];
                    if (a.Origen.AsignaturaId == b.Origen.AsignaturaId && a.Origen.AsignaturaId.HasValue) continue;
                    if (a.Contenidos.Length == 0 || b.Contenidos.Length == 0 || a.Creditos is null || b.Creditos is null
                        || a.Creditos + b.Creditos < destino.Creditos * 0.80m) continue;
                    var aporteA = Palabras(string.Join(" ", a.Contenidos.Select(x => x.Descripcion)));
                    var aporteB = Palabras(string.Join(" ", b.Contenidos.Select(x => x.Descripcion)));
                    aporteA.IntersectWith(palabrasDestino); aporteB.IntersectWith(palabrasDestino);
                    // Ambos orígenes deben aportar contenido pertinente y complementario.
                    if (!aporteA.Except(aporteB).Any() || !aporteB.Except(aporteA).Any()) continue;
                    await EvaluarAsync([a, b]);
                }
            }
            var viables = evaluadas.Where(EsViable).OrderByDescending(Cobertura)
                .ThenBy(x => x.Origenes.Length).ThenBy(x => x.Origenes[0].AsignaturaCursadaId).ToArray();
            if (viables.Length > 0)
            {
                propuestas.AddRange(viables);
                propuestas.AddRange(evaluadas.Where(EsCercana).OrderByDescending(Cobertura).Take(2));
            }
            else if (evaluadas.Count > 0)
            {
                // Un único diagnóstico por destino, separado de las recomendaciones en la interfaz.
                propuestas.Add(evaluadas.OrderByDescending(Cobertura).ThenBy(x => x.Origenes.Length).First());
            }
            else
            {
                var diagnostico = JsonSerializer.SerializeToElement(new
                {
                    reglas = new { resultado = 3, razonCreditos = (decimal?)null, coberturaContenidos = (decimal?)null,
                        razonHoras = (decimal?)null, versionReglas = "sin-evaluacion",
                        advertencias = new[] { "No se identificaron candidatos con afinidad académica. Revisa el historial y los sílabos." } },
                    cobertura = new { porcentaje = (decimal?)null, temasCoincidentes = Array.Empty<object>(), temasAusentes = Array.Empty<object>() },
                    explicacion = "Sin recomendación automática: no hay evidencia suficiente para proponer una equivalencia.", fuenteExplicacion = "plantilla"
                });
                propuestas.Add(new Propuesta(destino.Id, destino.Nombre, [], diagnostico));
            }

            async Task EvaluarAsync(Candidato[] grupo)
            {
                var temasOrigen = grupo.All(x => x.Contenidos.Length > 0)
                    ? grupo.SelectMany(x => x.Contenidos).DistinctBy(x => x.Codigo).ToArray() : [];
                var solicitud = new SolicitudReglas(
                    grupo.All(x => x.Creditos.HasValue) ? grupo.Sum(x => x.Creditos) : null, destino.Creditos,
                    grupo.All(x => x.Horas.HasValue) ? grupo.Sum(x => (decimal?)x.Horas) : null, destino.Horas,
                    true, grupo.Min(x => x.Origen.Nota), grupo.Length == 1 && grupo[0].Transversal, destino.EsTransversal,
                    temasOrigen, temasDestino);
                var evaluacion = await datos.EvaluarAsync(solicitud, bearer, ct);
                evaluadas.Add(new Propuesta(destino.Id, destino.Nombre,
                    grupo.Select(x => new OrigenPropuesta(x.Origen.Id, x.Origen.Nombre)).ToArray(), evaluacion));
            }
        }

        return new Analisis(propuestas, advertencias);

        async Task<Contenido[]> CargarContenidosAsync(Guid id)
        {
            if (!contenidos.TryGetValue(id, out var temas))
            {
                temas = await datos.ContenidosAsync(id, bearer, ct);
                contenidos.Add(id, temas);
            }
            return temas;
        }
    }

    private static bool EsViable(Propuesta propuesta) =>
        propuesta.Evaluacion.GetProperty("reglas").GetProperty("resultado").GetInt32() is 0 or 4;

    private static bool EsCercana(Propuesta p)
    {
        var r = p.Evaluacion.GetProperty("reglas");
        return r.GetProperty("resultado").GetInt32() is 1 or 2
            && r.TryGetProperty("razonCreditos", out var c) && c.ValueKind == JsonValueKind.Number && c.GetDecimal() >= .75m
            && r.TryGetProperty("coberturaContenidos", out var t) && t.ValueKind == JsonValueKind.Number && t.GetDecimal() >= .75m
            && r.TryGetProperty("razonHoras", out var h) && h.ValueKind == JsonValueKind.Number;
    }

    private static decimal Cobertura(Propuesta propuesta) =>
        propuesta.Evaluacion.GetProperty("reglas").TryGetProperty("coberturaContenidos", out var valor)
            && valor.ValueKind == JsonValueKind.Number ? valor.GetDecimal() : -1m;

    private static string Normalizar(string texto) => new string(texto.Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
        .ToArray()).ToLowerInvariant();

    private static readonly HashSet<string> Genericas = new(StringComparer.OrdinalIgnoreCase)
        { "para", "con", "del", "los", "las", "una", "por", "sus", "introduccion", "fundamentos", "basico", "basica", "taller", "aplicaciones" };
    private static HashSet<string> Palabras(string nombre) =>
        System.Text.RegularExpressions.Regex.Matches(Normalizar(nombre), @"[a-z0-9]+")
            .Select(x => x.Value).Where(x => x.Length > 2 && !Genericas.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed record Candidato(Origen Origen, decimal? Creditos, int? Horas, bool Transversal, Contenido[] Contenidos);
    internal sealed record OrigenPropuesta(Guid AsignaturaCursadaId, string Nombre);
    internal sealed record Propuesta(Guid AsignaturaDestinoId, string NombreDestino, OrigenPropuesta[] Origenes,
        JsonElement Evaluacion, IReadOnlyList<RevisionDocentePropuesta>? RevisionesDocente = null);
    internal sealed record RevisionDocentePropuesta(string Tipo, int ResultadoFinal,
        string? Observacion, DateTime RevisadoEn, Guid ProfesorId, string ProfesorNombre);
    internal sealed record Analisis(IReadOnlyList<Propuesta> Propuestas, IReadOnlyList<string> Advertencias);
}
