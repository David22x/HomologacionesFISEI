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
            grupos.Select(x => new OrigenPropuesta(x.Origen.Id, x.Origen.Nombre, x.Origen.AsignaturaId)).ToArray(), evaluacion);
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

        var fichas = new Dictionary<Guid, Asignatura?>();
        var contenidos = new Dictionary<Guid, Contenido[]>();
        var palabrasOrigen = origenes.ToDictionary(origen => origen.Id, origen => Palabras(origen.Nombre));
        foreach (var destino in destinos.OrderBy(x => x.Id))
        {
            if (origenes.Length == 0)
                break;

            var palabrasDestino = Palabras(destino.Nombre);
            var candidatos = origenes
                .OrderByDescending(x => x.AsignaturaId == destino.Id)
                .ThenByDescending(x => string.Equals(x.Nombre.Trim(), destino.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => palabrasOrigen[x.Id].Count(palabrasDestino.Contains))
                .ThenBy(x => x.Creditos is decimal creditos ? Math.Abs(creditos - destino.Creditos) : decimal.MaxValue)
                .ThenBy(x => x.Id)
                .Take(3)
                .ToArray();

            var destinoContenidos = await CargarContenidosAsync(destino.Id);
            var preparados = new List<Candidato>(candidatos.Length);
            foreach (var origen in candidatos)
            {
                Asignatura? ficha = null;
                Contenido[] temas = [];
                if (origen.AsignaturaId is Guid id)
                {
                    if (!fichas.TryGetValue(id, out ficha))
                    {
                        ficha = await datos.AsignaturaAsync(id, bearer, ct);
                        fichas.Add(id, ficha);
                    }

                    if (ficha is not null)
                        temas = await CargarContenidosAsync(id);
                }

                var candidato = new Candidato(origen, origen.Creditos ?? ficha?.Creditos,
                    origen.Horas ?? ficha?.Horas, ficha?.EsTransversal ?? false, temas);
                preparados.Add(candidato);
                await EvaluarAsync([candidato]);
            }

            for (var i = 0; i < preparados.Count; i++)
            for (var j = i + 1; j < preparados.Count; j++)
            {
                var primero = preparados[i];
                var segundo = preparados[j];
                if (primero.Origen.AsignaturaId is Guid id && id == segundo.Origen.AsignaturaId)
                    continue;

                await EvaluarAsync([primero, segundo]);
            }

            async Task EvaluarAsync(Candidato[] grupo)
            {
                var temasOrigen = grupo.All(x => x.Contenidos.Length > 0)
                    ? grupo.SelectMany(x => x.Contenidos).DistinctBy(x => x.Codigo).ToArray()
                    : [];
                var solicitud = new SolicitudReglas(
                    grupo.All(x => x.Creditos.HasValue) ? grupo.Sum(x => x.Creditos) : null,
                    destino.Creditos,
                    grupo.All(x => x.Horas.HasValue) ? grupo.Sum(x => (decimal?)x.Horas) : null,
                    destino.Horas, true, grupo.Min(x => x.Origen.Nota),
                    grupo.Length == 1 && grupo[0].Transversal, destino.EsTransversal,
                    temasOrigen, destinoContenidos);
                var evaluacion = await datos.EvaluarAsync(solicitud, bearer, ct);
                propuestas.Add(new Propuesta(destino.Id, destino.Nombre,
                    grupo.Select(x => new OrigenPropuesta(x.Origen.Id, x.Origen.Nombre, x.Origen.AsignaturaId)).ToArray(), evaluacion));
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

    private static HashSet<string> Palabras(string nombre) =>
        nombre.Split([' ', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed record Candidato(Origen Origen, decimal? Creditos, int? Horas, bool Transversal, Contenido[] Contenidos);
    internal sealed record OrigenPropuesta(Guid AsignaturaCursadaId, string Nombre, Guid? AsignaturaId = null);
    internal sealed record Propuesta(Guid AsignaturaDestinoId, string NombreDestino, OrigenPropuesta[] Origenes,
        JsonElement Evaluacion, IReadOnlyList<RevisionDocentePropuesta>? RevisionesDocente = null);
    internal sealed record RevisionDocentePropuesta(string Tipo, int ResultadoFinal,
        string? Observacion, DateTime RevisadoEn, Guid ProfesorId, string ProfesorNombre);
    internal sealed record Analisis(IReadOnlyList<Propuesta> Propuestas, IReadOnlyList<string> Advertencias);
}
