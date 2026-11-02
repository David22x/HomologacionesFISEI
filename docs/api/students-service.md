# Contrato del Students Service

Versión: `v1`  
Base local del servicio: `http://localhost:5194`  
Swagger UI en desarrollo: `http://localhost:5194/swagger`

Las rutas de estudiantes requieren un JWT válido enviado a través del API Gateway (base URL local `http://localhost:5080`). Los nombres JSON usan camelCase.

## Endpoints

| Método y ruta | Respuesta correcta | Errores esperados |
|---|---|---|
| `GET /api/v1/estudiantes/cedula/{cedula}` | `200` y un `EstudianteResponse` si la cédula coincide exactamente | `401` sin JWT válido; `404` si no existe o si el rol autenticado no tiene permiso |

La búsqueda está disponible para los roles `Administrador` y `Profesor`. Para cualquier otro rol autenticado, la respuesta es el mismo `404` genérico que para una cédula inexistente, sin consultar si el estudiante existe. El `traceId` de diagnóstico puede variar entre solicitudes. La búsqueda compara el identificador completo, no fragmentos.

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
