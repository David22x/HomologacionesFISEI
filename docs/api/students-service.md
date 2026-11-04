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
