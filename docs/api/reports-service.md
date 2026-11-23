# Reports Service

Servicio .NET 8 basado en la plantilla común: JWT, roles, Swagger, manejo de errores y `/health`. Puerto local `5198`; Gateway `5080`. Persiste los informes en SQL Server (`ReportsDB`) mediante EF Core. QuestPDF utiliza la licencia Community configurada para este proyecto académico.

## Configuración y ejecución

Comparte el almacén de User Secrets `HomologacionesFISEI-Local` y la configuración JWT de los demás servicios. Configura la conexión sin guardar contraseñas en el repositorio:

```powershell
$reportsProject = "backend/Services/Reports/ReportsService.csproj"
$reportsPassword = Read-Host "Contraseña local de SQL Server"
dotnet user-secrets set --project $reportsProject "ConnectionStrings:ReportsConnection" "Server=localhost,1433;Database=ReportsDB;User Id=sa;Password=$reportsPassword;TrustServerCertificate=True"
dotnet run --project $reportsProject
```

En Development aplica automáticamente la migración inicial; se puede desactivar con `Reports:MigrateOnStartup=false`. Para aplicar migraciones explícitamente:

```powershell
dotnet ef database update --project backend/Services/Reports/ReportsService.csproj
```

Las direcciones configurables son `HomologationService:BaseUrl`, `StudentsService:BaseUrl` y `AcademicService:BaseUrl` (por defecto puertos 5196, 5194 y 5193). Swagger: `http://localhost:5198/swagger`.

Docker Compose crea `ReportsDB`, aplica las migraciones en Development y configura las direcciones internas. Desde `infrastructure`, con `.env` configurado:

```powershell
docker compose up --build -d reports gateway
```

`REPORTS_PORT` permite cambiar el puerto publicado (por defecto 5198).

## Endpoints

Todas las rutas requieren `Authorization: Bearer <token>` y se publican también por el Gateway.

| Método | Ruta | Acceso | Respuesta |
| --- | --- | --- | --- |
| POST | `/api/v1/informes/expedientes/{id}/generar` | Profesor responsable | 201 nueva versión; 200 si ya existe para los mismos datos |
| GET | `/api/v1/informes/expedientes/{id}` | Profesor responsable o administrador | Versiones ordenadas de mayor a menor |
| GET | `/api/v1/informes/expedientes/{id}/{version}/pdf` | Profesor responsable o administrador | Archivo `application/pdf` |

La generación no requiere cuerpo. Devuelve `id`, `version`, `generadoEn` y `huellaSha256`; una versión nueva incluye `Location` hacia su descarga. El listado agrega `generadoPor`.

Homologation publica `GET /api/v1/homologacion/expedientes/{id}/informe`: devuelve el expediente, sus decisiones vigentes y el análisis guardado. Exige estado **Resuelto** y respeta la autorización del expediente. Reports revalida este acceso en cada operación y propaga el JWT a los servicios consultados.

## Contenido y versiones

El PDF incluye estudiante, identificación, carrera y malla de destino, profesor responsable, fecha UTC, decisiones por asignatura, materias de origen (también compuestas), resultado calculado, decisión docente, justificación, porcentajes disponibles, versión de reglas, explicación, advertencias y temas coincidentes/ausentes con evidencias. Los porcentajes desconocidos aparecen como «Sin datos». Incluye cabecera y numeración en todas las páginas.

Cada registro conserva el PDF original, una instantánea JSON de sus datos, autor, fecha UTC, versión y dos huellas SHA-256:

- `HuellaFuente`: identifica los datos de entrada. Repetir la solicitud con los mismos datos recupera su versión existente. Un cambio de datos crea una versión nueva sin modificar las anteriores.
- `HuellaSha256`: identifica los bytes del PDF. La descarga comprueba esta huella y devuelve `X-Content-SHA256` y `ETag`; una discrepancia devuelve 500.

Las versiones son correlativas por expediente. La generación se guarda en una transacción serializable y hay índices únicos por expediente/versión y expediente/huella de origen. Ante una colisión concurrente se devuelve 409: el cliente puede repetir la generación para recuperar el informe ya creado. Las huellas verifican integridad; no constituyen una firma digital.

## Errores y pruebas

401 sin autenticación; 403 por rol; 404 para un expediente ajeno, inexistente o una versión inexistente; 409 si no está resuelto o hay una generación concurrente; 502 por datos inválidos; 503 si los servicios consultados no están disponibles; 504 ante timeout.

```powershell
dotnet test tests/backend/ReportsService.Tests/ReportsService.Tests.csproj -c Release
dotnet test backend/HomologacionesFISEI.sln -c Release
```

Las pruebas de integración usan SQLite y respuestas HTTP controladas para los servicios fuente; generan PDFs reales con QuestPDF. Verifican permisos, estado requerido, reintentos, nuevas versiones, conservación del PDF anterior y detección de alteraciones. Incluyen un informe extenso para comprobar la paginación.
