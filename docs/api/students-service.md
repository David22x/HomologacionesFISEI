# Contrato del Students Service

Versión: `v1`  
Base local del servicio: `http://localhost:5194`  
Swagger UI en desarrollo: `http://localhost:5194/swagger`

Las rutas públicas de estudiantes requieren un JWT válido enviado a través del API Gateway (base URL local `http://localhost:5080`). La ruta interna de importación requiere el mismo JWT, pero se invoca directamente desde Documents Service y no se publica por el Gateway. Los nombres JSON usan camelCase.

## Endpoints

| Método y ruta | Respuesta correcta | Errores esperados |
|---|---|---|
| `GET /api/v1/estudiantes/cedula/{cedula}` | `200` y un `EstudianteResponse` si la cédula coincide exactamente | `401` sin JWT válido; `404` si no existe o si el rol autenticado no tiene permiso |
| `PUT /internal/estudiantes/importaciones` | `201` al crear el estudiante externo; `200` al actualizarlo o repetir la misma importación | `400` si el estudiante/historial no es válido; `401` sin JWT válido; `403` sin rol permitido; `409` si la identificación pertenece a un estudiante interno |

La búsqueda está disponible para los roles `Administrador` y `Profesor`. Para cualquier otro rol autenticado, la respuesta es el mismo `404` genérico que para una cédula inexistente, sin consultar si el estudiante existe. El `traceId` de diagnóstico puede variar entre solicitudes. La búsqueda compara el identificador completo, no fragmentos.


La ruta interna de importación también requiere JWT y rol `Administrador` o `Profesor`; solo la consume Documents Service directamente y no se publica por el API Gateway. Recibe el mismo esquema `EstudianteWriteRequest` usado por `POST /api/v1/estudiantes`, con `tipo: 1` (`Externo`) y un historial cuyas asignaturas usan `asignaturaId: null` y `codigo`. La identificación se normaliza para buscar el registro existente. Las confirmaciones repetidas no duplican historial ni asignaturas; cuando el contenido cambia, se actualizan los datos existentes y se reemplaza la colección de asignaturas importadas.

## Esquema de respuesta

```json
{
  "id": "guid",
  "cedula": "string",
  "tipoIdentificacion": 0,
  "nombres": "string",
  "apellidos": "string",
  "tipo": 0,
  "creadoPor": "string o null",
  "creadoEn": "fecha ISO-8601",
  "modificadoPor": "string o null",
  "modificadoEn": "fecha ISO-8601",
  "historial": null
}
```

Cuando el estudiante tiene historial, `historial` incluye sus datos académicos y asignaturas cursadas.


## Carga por nivel, edición y eliminación del registro activo

El formulario solicita carrera y malla antes de precargar las asignaturas de los niveles 1 hasta el nivel actual inclusive. La nota y el período quedan vacíos para completarlos explícitamente; código, nombre, créditos y horas son editables. Se pueden quitar y agregar materias del catálogo. En edición, la carga masiva es una acción explícita que agrega solamente las materias faltantes y conserva las existentes. `historial.mallaOrigenId` conserva la selección de malla y es opcional para mantener la compatibilidad con registros anteriores.

Al actualizar el historial se conservan los identificadores de las materias existentes; las nuevas se registran explícitamente como altas de EF y las retiradas se eliminan del historial. Esto evita errores de concurrencia al agregar una materia a un estudiante existente.

`DELETE /api/v1/estudiantes/{id}` exige rol Profesor o Administrador y devuelve 204. Realiza una baja lógica, conservando historial y referencias de expedientes. El estudiante deja de aparecer en el listado y la búsqueda por cédula; no puede editarse ni utilizarse para crear nuevos expedientes. La consulta autorizada por ID conserva acceso a los datos de expedientes previos. La identificación continúa reservada. La interfaz solicita confirmación antes de eliminar.

La migración `StudentLifecycleAndOriginMalla` agrega `Estudiantes.Eliminado` y `HistorialesAcademicos.MallaOrigenId`. Se aplica al arrancar Students en Development; para otros entornos, ejecutar `dotnet ef database update --project backend/Services/Students/StudentsService.csproj` con la conexión configurada.
