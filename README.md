# Sistema de Homologación FISEI

## Entorno local: base de datos (SQL Server en Docker)

### Requisitos
- Docker Desktop con el motor en ejecución ("Engine running").
- Git.
- .NET SDK y Node.js, para los servicios y el frontend (se documentan aquí a medida que se agregan).

### Levantar SQL Server con AuthDB

    cd infrastructure
    cp .env.example .env
    docker compose up -d
    docker compose ps -a

Espera a que `sqlserver` esté `healthy`. El contenedor `sqlserver-init` corre una sola vez, crea las bases y termina con `Exited (0)`. Cambia la clave en tu `.env` si quieres; ese archivo no se sube al repositorio.

### Verificar que AuthDB existe

    docker compose exec -T sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "SELECT name FROM sys.databases"'

En Git Bash de Windows, antepón `MSYS_NO_PATHCONV=1` al comando.

### Cadena de conexión para los servicios

    Server=localhost,1433;Database=AuthDB;User Id=sa;Password=<MSSQL_SA_PASSWORD de tu .env>;TrustServerCertificate=True

### Detener, reiniciar y limpiar
- `docker compose down`: detiene todo y conserva los datos.
- `docker compose down -v`: detiene todo y borra los datos.

## Integración continua

El workflow de GitHub Actions en `.github/workflows/ci.yml` valida el frontend y el backend en cada push a `main`, `develop` o una rama `feature/**`, y en los pull requests dirigidos a `main` o `develop`.

- Frontend: `npm ci`, `npm run build` y `npm test -- --run` desde `frontend`.
- Backend: `dotnet restore` y `dotnet build` de `backend/HomologacionesFISEI.sln`, más las pruebas xUnit de `tests/backend/BuildingBlocks.Tests`.

### Problemas comunes
- Error al conectar con la API de Docker: abre Docker Desktop y espera a "Engine running".
- Puerto 1433 ocupado: cambia `MSSQL_PORT` en tu `.env` (por ejemplo a 1434) y ajusta la cadena de conexión.
- SQL Server no arranca: la clave debe tener mayúsculas, minúsculas, números y símbolos.

## Auth Service, API Gateway y frontend

Con SQL Server disponible y `AuthDB` creada, inicia Auth Service y el Gateway
en terminales separadas desde la raíz del repositorio:

    dotnet run --project backend/Services/Auth/AuthService.csproj
    dotnet run --project backend/Services/ApiGateway/ApiGateway.csproj

Auth Service escucha en `http://localhost:5192` y el Gateway en
`http://localhost:5080`. El Gateway enruta `POST /auth/login` y `GET /auth/me`
al servicio de Auth, permite CORS desde el origen local de Vite
`http://localhost:5173`.

El frontend debe enviar sus solicitudes de autenticación a través del Gateway,
por ejemplo `POST http://localhost:5080/auth/login` y
`GET http://localhost:5080/auth/me`. El segundo endpoint requiere el token
Bearer obtenido al iniciar sesión. Los errores de conectividad con Auth se
devuelven como respuestas de Gateway con estado `502 Bad Gateway`.
