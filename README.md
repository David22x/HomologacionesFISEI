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

### Problemas comunes
- Error al conectar con la API de Docker: abre Docker Desktop y espera a "Engine running".
- Puerto 1433 ocupado: cambia `MSSQL_PORT` en tu `.env` (por ejemplo a 1434) y ajusta la cadena de conexión.
- SQL Server no arranca: la clave debe tener mayúsculas, minúsculas, números y símbolos.

### Pendiente de documentar
Auth Service, API Gateway y frontend (HU-S1-01).
