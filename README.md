# Sistema de Homologación FISEI

## Estructura del repositorio

- `frontend/`: aplicación React, TypeScript y Vite.
- `backend/Services/`: Auth Service, Academic Service, API Gateway y plantilla de microservicio.
- `backend/BuildingBlocks/`: autenticación JWT, roles, Swagger y manejo común de errores.
- `database/init/`: scripts iniciales de SQL Server.
- `infrastructure/`: configuración de Docker Compose.
- `tests/backend/`: pruebas automatizadas de los servicios y componentes compartidos.

## Requisitos locales

- Docker Desktop con el motor iniciado.
- .NET SDK 8.
- Node.js 22 o superior y npm.
- Git.

## Preparar SQL Server y AuthDB

Desde PowerShell, en la raíz del repositorio:

```powershell
Set-Location .\infrastructure
Copy-Item .env.example .env
```

Edita `infrastructure\.env` y reemplaza `MSSQL_SA_PASSWORD` por una contraseña segura. Luego inicia SQL Server:

```powershell
docker compose up -d
docker compose ps -a
```

Espera hasta que `sqlserver` tenga estado `healthy`. `sqlserver-init` crea `AuthDB` y termina con código 0. La base de datos conserva sus datos mientras exista el volumen Docker.

## Configurar secretos locales de .NET

Las contraseñas y la clave JWT no están guardadas en el repositorio. Auth Service, API Gateway y Template Service comparten el almacén local de .NET User Secrets. Configúralo una vez desde PowerShell, en la raíz del repositorio:

```powershell
$jwtSecret = Read-Host "Clave JWT aleatoria (mínimo 32 caracteres)"
$saPassword = Read-Host "MSSQL_SA_PASSWORD configurada en infrastructure\.env"
$adminPassword = Read-Host "Contraseña local de admin@uta.edu.ec"
$professorPassword = Read-Host "Contraseña local de profesor@uta.edu.ec"

$connectionString = "Server=localhost,1433;Database=AuthDB;User Id=sa;Password=$saPassword;TrustServerCertificate=True"
$academicConnectionString = "Server=localhost,1433;Database=AcademicDB;User Id=sa;Password=$saPassword;TrustServerCertificate=True"
$authProject = ".\backend\Services\Auth\AuthService.csproj"
$academicProject = ".\backend\Services\Academic\AcademicService.csproj"

dotnet user-secrets set --project $authProject "Jwt:SecretKey" $jwtSecret
dotnet user-secrets set --project $authProject "ConnectionStrings:DefaultConnection" $connectionString
dotnet user-secrets set --project $authProject "SeedUsers:AdministradorPassword" $adminPassword
dotnet user-secrets set --project $authProject "SeedUsers:ProfesorPassword" $professorPassword
dotnet user-secrets set --project $academicProject "ConnectionStrings:AcademicConnection" $academicConnectionString
```

Usa una clave JWT aleatoria de al menos 32 caracteres. Las contraseñas iniciales deben cumplir las reglas de Identity (mínimo 6 caracteres y al menos un dígito). Auth Service y Academic Service comparten el almacén local de User Secrets, por lo que la clave JWT configurada para Auth también queda disponible para Academic. Los secretos se guardan localmente fuera del repositorio. El usuario `admin@uta.edu.ec` tiene el rol `Administrador` y `profesor@uta.edu.ec` tiene el rol `Profesor`; sus contraseñas son las que acabas de configurar.

Si cambias las contraseñas luego de que los usuarios ya se sembraron, los usuarios existentes no se modifican automáticamente. Para reiniciar el entorno de desarrollo desde cero, detén Compose y elimina sus datos con `docker compose down -v`; después levanta la base y ejecuta Auth Service otra vez.

## Ejecutar backend y frontend

Con SQL Server activo y los User Secrets configurados, abre terminales separadas desde la raíz:

**Terminal 1 — Auth Service** (aplica las migraciones y crea las tablas de Identity en `AuthDB`; puerto 5192):

```powershell
dotnet run --project .\backend\Services\Auth\AuthService.csproj
```

**Terminal 2 — API Gateway** (puerto 5080):

```powershell
dotnet run --project .\backend\Services\ApiGateway\ApiGateway.csproj
```

**Terminal 3 — Academic Service** (puerto 5193):

```powershell
dotnet run --project .\backend\Services\Academic\AcademicService.csproj
```

El Gateway enruta el login sin token y valida el JWT para `GET /auth/me`. El Auth Service también valida el token y comprueba que el usuario siga activo. Swagger de Auth está en `http://localhost:5192/swagger`.

**Terminal 4 — Frontend**:

```powershell
Set-Location .\frontend
npm ci
npm run dev
```

Vite muestra la URL local, normalmente `http://localhost:5173`. La aplicación usa el Gateway (`http://localhost:5080`) como API y, después del login, consulta `/auth/me` para mostrar el nombre y rol actuales.

`TemplateService` es solamente una plantilla sin rutas de negocio y no está expuesta por el Gateway. Si necesitas iniciarla para desarrollo:

```powershell
dotnet run --project .\backend\Services\TemplateService\TemplateService.csproj
```

El servicio usa el mismo secreto JWT compartido y escucha en `http://localhost:5158`.

## Preparación para HU-S1-02 (acceso por rol)

Los roles `Administrador` y `Profesor` están centralizados en `backend/BuildingBlocks/Roles/AppRoles.cs`; Auth Service los crea y los incluye como claims del JWT. El Gateway ya exige un JWT válido para las rutas protegidas configuradas, solo publica las rutas declaradas explícitamente en `backend/Services/ApiGateway/ocelot.json` y responde 404 a cualquier ruta bajo `/internal` (sin distinguir mayúsculas/minúsculas).

Al agregar funciones en HU-S1-02, cada ruta protegida deberá declarar su autenticación en Ocelot y aplicar autorización por rol también en el controlador o endpoint del microservicio. Las rutas internas no deben agregarse al Gateway. La pantalla principal actual todavía no incluye un menú por rol ni rutas de negocio; esos comportamientos quedan para la implementación de esa historia.

## API de autenticación

| Método y ruta | Descripción | Respuestas principales |
|---|---|---|
| `POST /auth/login` | Recibe `{ "email", "password" }` a través del Gateway y devuelve token y perfil | 200, 400, 401 |
| `GET /auth/me` | Devuelve el perfil y roles del token enviado | 200, 401 |

Una credencial incorrecta, una cuenta inactiva o bloqueada devuelve 401 sin distinguir cuál fue la causa. La pantalla de login muestra un mensaje genérico. La cuenta se bloquea tras cinco intentos fallidos consecutivos.

## Detener el entorno

Desde `infrastructure/`:

```powershell
docker compose down
```

Esto conserva los datos. Para borrar también la base de datos local:

```powershell
docker compose down -v
```

## Pruebas y CI

Desde la raíz, ejecuta las pruebas del backend:

```powershell
dotnet test .\backend\HomologacionesFISEI.sln
```

Para compilar y probar el frontend:

```powershell
Set-Location .\frontend
npm ci
npm run build
npm test -- --run
```

El workflow `.github/workflows/ci.yml` ejecuta la instalación, compilación y pruebas de ambos proyectos en GitHub Actions. Las pruebas de Auth usan una base en memoria y no requieren Docker ni los secretos locales.

## Problemas comunes

- **Docker no responde:** inicia Docker Desktop y espera a que indique que el motor está listo.
- **Puerto 1433 ocupado:** cambia `MSSQL_PORT` en `infrastructure\.env` y actualiza el puerto en `ConnectionStrings:DefaultConnection`.
- **SQL Server no inicia:** verifica que la contraseña de `MSSQL_SA_PASSWORD` cumpla los requisitos de SQL Server.
- **Un servicio informa que falta configuración:** configura las claves `ConnectionStrings:DefaultConnection` para Auth, `ConnectionStrings:AcademicConnection` para Academic, `Jwt:SecretKey` y las contraseñas de semilla de Auth con los comandos de User Secrets anteriores.
- **Academic Service no puede conectarse a SQL Server:** verifica que `sqlserver` esté `healthy`, que `AcademicDB` exista y que `ConnectionStrings:AcademicConnection` use el puerto y la contraseña configurados en `infrastructure\.env`.
- **El login da error de conexión:** confirma que SQL Server está `healthy`, que `AuthDB` existe y que Auth Service y Gateway siguen ejecutándose.
