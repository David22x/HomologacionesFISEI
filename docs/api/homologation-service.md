# Homologation Service: expedientes

Los endpoints se consumen a través del API Gateway y requieren JWT. El profesor crea expedientes; Administrador y Profesor pueden consultarlos. Cada profesor ve sus expedientes y Administrador puede ver todos.

| Método y ruta | Descripción | Respuestas principales |
|---|---|---|
| `POST /api/v1/homologacion/expedientes` | Crea expediente en estado `Borrador` | 201, 400, 401, 403, 404, 503 |
| `GET /api/v1/homologacion/expedientes` | Lista expedientes visibles para el rol | 200, 401, 403 |

Antes de guardar, Homologation consulta Students Service para verificar el estudiante y Academic Service para verificar que la malla pertenezca a la carrera indicada. Las referencias entre servicios son identificadores lógicos; no hay claves foráneas entre bases de datos. Si un servicio dependiente no está disponible, la creación se rechaza y no se guarda el expediente.

## Pares y evaluación por reglas

Las rutas parten de `/api/v1/homologacion/expedientes/{id}`. El profesor asignado puede editar y evaluar expedientes en `Borrador` o `EnRevision`. El administrador puede consultar los pares de cualquier expediente. Un expediente ajeno al profesor responde 404.

| Método y ruta relativa | Operación |
|---|---|
| `GET /pares` | Devuelve los pares persistidos. |
| `POST /pares/precargar` | Agrega coincidencias únicas por nombre completo, ignorando mayúsculas y espacios al inicio y al final. Omite nombres ambiguos y conserva los pares guardados. |
| `PUT /pares` | Reemplaza la selección. Un arreglo vacío elimina todos los pares. |
| `POST /evaluar-reglas` | Evalúa los pares guardados y devuelve los resultados y el ID de ejecución. Guarda la ejecución y cambia el expediente a `EnRevision` en la misma transacción. |

Cuerpo de ejemplo para `PUT /pares` (sustituir los identificadores por los del historial y la malla):

```json
[
  {
    "asignaturaCursadaId": "11111111-1111-1111-1111-111111111111",
    "asignaturaDestinoId": "22222222-2222-2222-2222-222222222222"
  }
]
```

Homologation obtiene créditos, horas, nota y la marca transversal de los servicios académicos. El historial se trata como una lista de asignaturas aprobadas, según el contrato de Students Service. La nota mínima sigue sin configurar. Los temas se ponderan con sus horas estimadas y sus subtemas aportan contexto a la comparación. Si falta contenido externo, las reglas determinan el resultado con los datos disponibles y devuelven `DatosIncompletos` cuando necesitan comparar contenidos. La evaluación de esta tarea se realiza por par individual. Si falla un servicio, la operación devuelve un error y conserva el estado y las ejecuciones previas.

Agent IA expone `POST /api/v1/agent-ia/evaluar-reglas` detrás del Gateway y requiere JWT de profesor. En Docker usa `GEMINI_API_KEY` de `infrastructure/.env`. Para ejecución local, iniciar Agent IA con el mismo secreto JWT que Homologation, configurar `Gemini:ApiKey` mediante User Secrets y ejecutar:

```powershell
dotnet run --project backend/Services/AgentIa/AgentIaService.csproj
```

Homologation usa `http://localhost:5197` por defecto. En Docker, Compose configura la URL interna `http://agent-ia:5197`.

## Generación de propuestas con IA

`POST /api/v1/homologacion/expedientes/{id}/analizar-ia` no requiere cuerpo. Debe usar el JWT del profesor asignado. El análisis parte del historial y de la malla del expediente. Devuelve `id`, `ejecutadoEn`, `estado`, `propuestas` y `advertencias`.

Por cada destino, se preseleccionan hasta tres materias. El orden prioriza la misma referencia de catálogo, el nombre exacto, las palabras en común y la cercanía de créditos. El identificador resuelve empates. Cada candidata se evalúa por separado y se generan las combinaciones de dos materias distintas. Las compuestas suman los créditos y las horas disponibles de ambas materias y evalúan la unión de los contenidos. Si falta un dato obligatorio en uno de los orígenes, R4 determina si el resultado es `DatosIncompletos` o si ya procede un rechazo por créditos.

Al completar el análisis se guarda una ejecución y el expediente pasa a `AnalizadoConIa`. Un historial o una malla sin materias devuelve una lista vacía con advertencias. Un reintento posterior a un análisis guardado devuelve la misma ejecución. Si un servicio falla durante el proceso, se conserva el estado previo y se puede reintentar. Si dos peticiones se cruzan y una devuelve 409, reintentar permite recuperar la ejecución guardada.
