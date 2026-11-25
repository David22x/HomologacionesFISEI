# Verificación del flujo y preparación de la presentación

## Cambios de estabilización

- Swagger de Documents genera las operaciones multipart para XLSX y PDF. Los archivos usan la inferencia `IFormFile`, sin la combinación de `[FromForm]` que bloqueaba Swagger.
- La nota mínima se obtiene de configuración, se valida y se conserva junto con los parámetros de la ejecución. No se asigna un mínimo que el docente no haya confirmado.
- La revisión de expedientes muestra accesos **Revisar siguiente pendiente** y **Revisar destino**. Abren también los diagnósticos ocultos. Una selección no es una decisión docente; la resolución exige decisiones concluyentes para todos los destinos analizados.
- El verificador local permite ensayar el caso completo sin tocar registros anteriores: crea su propio catálogo, estudiantes y expediente QA.

## Actualizar el entorno desplegado

Desde la raíz del repositorio, con Docker Desktop iniciado y `infrastructure/.env` configurado:

```powershell
docker compose -f infrastructure/docker-compose.yml up -d --build agent-ia documents homologation reports gateway frontend
docker compose -f infrastructure/docker-compose.yml ps
```

Es necesario reconstruir los contenedores para que las correcciones del código estén disponibles en las APIs y en el navegador. No usar `down -v`: elimina los datos.

Comprobar `http://localhost:5104/swagger/v1/swagger.json` después del despliegue. Agent IA y Ollama no publican necesariamente sus puertos al host; comprobarlos desde Docker o a través del Gateway. La ausencia de un puerto localhost no demuestra que estén detenidos.

## Nota mínima

En `infrastructure/.env`, `HOMOLOGACION_NOTA_MINIMA=` mantiene el mínimo numérico sin configurar. Solo después de confirmar el valor con el docente, asignar el número en escala 0–10 y recrear Agent IA. En ejecución manual se usa `Homologacion__Reglas__NotaMinima`. Valores fuera de 0–10 o no numéricos impiden el arranque con un error claro.

Los créditos y contenidos mantienen sus mínimos de 80 %. La similitud de contenidos mantiene 0,75. Las horas insuficientes generan advertencia. Los acuerdos normativos pendientes siguen identificados en el catálogo; no se presentan como aprobados.

## Pruebas de código

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:VSTEST_TELEMETRY_OPTEDIN='0'
dotnet test backend/HomologacionesFISEI.sln -c Release
npm --prefix frontend run build
npm --prefix frontend test -- --run
```

En el entorno restringido se verificó la suite sin restaurar paquetes, con la telemetría desactivada y un proxy de salida cerrado, exceptuando localhost. Esto conserva las conexiones locales usadas por las pruebas y evita el envío de telemetría del SDK.

## Prueba funcional reproducible

Requiere Python 3 sin paquetes adicionales, Gateway y servicios iniciados, y las contraseñas de desarrollo `ADMIN_PASSWORD` y `PROFESSOR_PASSWORD` en el archivo `.env` local. No imprime ni conserva contraseñas o tokens. Solo admite un Gateway en localhost.

```powershell
# Consultas autenticadas, sin crear registros:
python scripts/verificar_flujo.py

# Crea datos sintéticos QA y verifica el recorrido completo:
python scripts/verificar_flujo.py --crear-caso

# Prueba generativa independiente sin crear estudiantes ni expedientes:
python scripts/verificar_flujo.py --probar-ia-documentos
```

`--crear-caso` crea dos carreras QA con una asignatura y sus contenidos, publica las mallas, lee un PDF sintético, registra su estudiante y repite la operación para detectar duplicados. Valida y confirma un XLSX externo con temas y subtemas. Después crea un expediente, precarga coincidencias, evalúa reglas, analiza con Ollama, guarda selección y decisión, resuelve, comprueba el bloqueo y la auditoría, genera el PDF y verifica la huella SHA-256. Repetir la generación no debe crear una versión adicional.

El caso QA se conserva deliberadamente para demostrarlo en la interfaz. Cada ejecución con `--crear-caso` crea un caso nuevo y aislado. No modifica los estudiantes o expedientes previos. El resultado y los IDs creados se registran progresivamente en `obj/verificacion-flujo/resultado.json`. Si se interrumpe, ese archivo identifica qué pasos llegaron a completarse y qué registros existen.

La opción generativa utiliza datos sintéticos y exige que Ollama extraiga al menos una nota correctamente. Un formulario manual de respaldo se reporta como extracción no completada; no se confunde HTTP 200 con éxito del modelo. Los PDF escaneados requieren OCR previo.

## Recorrido en pantalla

1. Profesor: localizar el estudiante de prueba y abrir su expediente QA.
2. Mostrar coincidencias por reglas y propuestas IA con su evidencia.
3. Explicar la tabla definitiva fija y la diferencia entre selección y aprobación.
4. En un expediente pendiente, usar **Revisar siguiente pendiente**, justificar conclusiones propias o rechazos y completar los destinos.
5. Resolver y mostrar el bloqueo de edición.
6. Generar y descargar el PDF; repetir para mostrar que no se duplica la versión.

Para una exposición breve, usar una malla de demostración pequeña. Mantener los modelos descargados antes de comenzar y conservar un expediente ya resuelto como respaldo.
