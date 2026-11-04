# Contrato del Academic Service

Versión: `v1`  
Base local del servicio: `http://localhost:5193`  
Swagger UI en desarrollo: `http://localhost:5193/swagger`

Todas las rutas requieren un JWT válido en `Authorization: Bearer <token>` y el rol `Administrador` o `Profesor`. Enviar la solicitud a través del API Gateway (base URL local `http://localhost:5080`) es el flujo usado por el frontend. Los nombres JSON usan camelCase.

## Endpoints

| Método y ruta | Respuesta correcta | Errores esperados |
|---|---|---|
| `GET /api/v1/catalogo/carreras` | `200` y lista de `CarreraCatalogoResponse` | `401` sin token válido; `403` con un rol sin permiso |
| `GET /api/v1/catalogo/carreras/{carreraId}/mallas` | `200` y lista de `MallaCatalogoResponse` | `401`, `403`, `404` si no existe la carrera |
| `GET /api/v1/catalogo/mallas/{mallaId}/niveles` | `200` y lista de `NivelCatalogoResponse` | `401`, `403`, `404` si no existe la malla |
| `GET /api/v1/catalogo/mallas/{mallaId}/asignaturas` | `200` y lista de `AsignaturaCatalogoResponse` | `400` si un filtro no tiene formato válido; `401`, `403`, `404` si no existe la malla |

Las consultas que no encuentran filas devuelven `200` con una lista vacía. Los identificadores de ruta deben ser GUID. Las consultas son de solo lectura.

## Parámetros de asignaturas

Los tres filtros son opcionales y pueden combinarse; si se combinan, deben cumplirse todos:

| Parámetro | Tipo | Comportamiento |
|---|---|---|
| `nivelId` | GUID | Filtra por el identificador del nivel |
| `nivel` | entero | Filtra por número de nivel |
| `search` | texto | Busca sin distinguir mayúsculas/minúsculas en código, nombre y unidad organizacional; se recortan espacios al inicio y al final |

## Esquemas JSON

Las respuestas son arreglos JSON con estas propiedades:

```json
{
  "id": "guid",
  "codigo": "string",
  "nombre": "string",
  "esDemo": false
}
```

```json
{
  "id": "guid",
  "carreraId": "guid",
  "version": "string",
  "vigente": true
}
```

```json
{
  "id": "guid",
  "mallaVersionId": "guid",
  "numero": 1
}
```

```json
{
  "id": "guid",
  "mallaVersionId": "guid",
  "nivelId": "guid",
  "nivelNumero": 1,
  "codigo": "string",
  "nombre": "string",
  "creditos": 3,
  "horas": 144,
  "unidadOrganizacional": "string o null"
}
```

Los campos `creditos` y `horas` representan los valores almacenados en el catálogo. La unicidad de `codigo` se evalúa dentro de una misma versión de malla.

## Datos semilla del Sprint 1

La semilla contiene las 40 asignaturas de TI y las 40 de SW, además de estas carreras demo sin mallas ni asignaturas:

| Código | Carrera | `esDemo` |
|---|---|---|
| `IND` | Industrial | `true` |
| `ROB` | Robótica | `true` |
| `TEL` | Telecomunicaciones | `true` |

El Academic Service aplica migraciones y carga la semilla al iniciar únicamente en el entorno `Development` (salvo que `Academic:SeedOnStartup` se desactive). Reinicia ese servicio después de actualizar el JSON. La interfaz muestra `(DEMO)` junto al nombre de estas carreras.

Para confirmar en SQL Server Management Studio que se cargaron en `AcademicDB`:

```sql
SELECT Codigo, Nombre, EsDemo
FROM dbo.Carreras
WHERE EsDemo = 1
ORDER BY Nombre;
```

No se deben completar sus planes de estudio con datos supuestos.

## Pruebas de contrato e integración

`CatalogoApiIntegrationTests` levanta el Academic Service real con una base SQLite aislada, carga la semilla versionada y comprueba navegación, filtros, respuestas, permisos y el documento OpenAPI generado. No requiere SQL Server, Docker ni claves locales. Estas pruebas validan el servicio HTTP y su contrato; la conectividad física a SQL Server y el enrutamiento entre Gateway y Academic Service se verifican al ejecutar el entorno local.
