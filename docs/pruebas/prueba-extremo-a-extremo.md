# Prueba de extremo a extremo del informe de homologación

Guía para validar el flujo completo, desde un entorno limpio hasta el informe PDF. Corresponde a SDH-93 (HU-S3-16, T16.5).

## Requisitos

- Docker Desktop con el motor iniciado y al menos 25 GB libres en el disco. Los modelos de Ollama ocupan unos 6 GB (ver [README](../../README.md)).
- Archivo `infrastructure/.env` configurado a partir de `.env.example`.
- Rama `develop` actualizada, con los cambios del informe fusionados.
- Para el caso externo: `estudiante_externo_prueba_v2.xlsx`, con subtemas en todos los temas, y la guía [importaciones académicas](../importaciones-academicas.md).

## 1. Entorno limpio

```powershell
Set-Location .\infrastructure
docker compose down -v
docker compose up --build -d
docker compose ps
```

Todos los servicios deben quedar `healthy`. `sqlserver-init` y `ollama-models` terminan en `Exited` sin error. La primera construcción tarda varios minutos y la descarga de modelos depende de la conexión.

## 2. Caso interno (Tecnologías de la Información a Software)

1. Iniciar sesión como profesor.
2. En Estudiantes, crear un estudiante interno de TI. Al indicar el nivel actual se cargan las asignaturas; completar nota y período de cada una y guardar.
3. Crear un expediente hacia la carrera Software, malla 2026.
4. Ejecutar el análisis de IA. Esperar a que termine.
5. Registrar una decisión para cada asignatura de destino. Cuando todos los destinos tienen decisión, pulsar **Resolver**. El expediente pasa a **Resuelto**.
6. En la sección **Informe PDF**, pulsar **Generar informe**. Debe aparecer la versión 1.
7. Pulsar **Generar informe** otra vez sin cambiar nada. Debe indicar que no se creó una versión nueva.
8. Descargar la versión 1 y revisar el PDF (ver la lista de verificación).

## 3. Caso externo

1. Importar el estudiante externo con `estudiante_externo_prueba_v2.xlsx` (hojas Estudiante, Asignaturas y Contenidos).
2. Comprobar que el estudiante queda listo para homologar y que conserva los temas y subtemas de cada asignatura.
3. Crear el expediente hacia Software y repetir los pasos 4 a 8 del caso interno.
4. Confirmar que el análisis usa los temas del XLSX como contenidos de origen. Si faltan subtemas, el análisis responde que falta información; es el comportamiento esperado.

## 4. Lista de verificación del PDF

- Logo de la UTA y encabezado azul, con la versión y los primeros 8 caracteres del expediente.
- Datos del estudiante, tipo de homologación (interna o externa) y carrera de origen.
- Totales: decisiones, homologadas y rechazadas, que coinciden con las decisiones registradas.
- Tabla de asignaturas homologadas con créditos, contenidos y quién decidió (Sistema o Profesor). Las transversales directas muestran contenidos como «No aplica».
- Sección de no homologadas por decisión del profesor: solo las que el sistema recomendaba homologar y el profesor rechazó, con porcentajes, temas ausentes y justificación. Las demás rechazadas se cuentan en una línea.
- Observaciones del profesor cuando cambió el resultado calculado o dejó una nota.
- Total de asignaturas homologadas, campo de observaciones y firmas del profesor y del estudiante.
- Fechas en hora de Ecuador y sin identificadores internos.

## 5. Permisos y errores esperados

- Con rol administrador se pueden listar y descargar versiones, pero no generar (403).
- Un expediente que no está Resuelto no genera informe (409).
- Resolver exige decisión para todos los destinos; si falta alguna, el expediente no se resuelve.

## 6. Registro de resultados

| Caso | Fecha | Responsable | Resultado | Observaciones |
| --- | --- | --- | --- | --- |
| Interno (TI a SW) |  |  |  |  |
| Externo (XLSX) |  |  |  |  |

## Pendientes conocidos

- Regla R3 (horas): el docente debe confirmar si las horas se exigen al 80 % o solo se muestran como referencia. Ver el [catálogo de reglas](../homologacion/catalogo_reglas_homologacion_v1.md).
- «Evaluar por reglas» respondió 503 una vez durante una prueba previa, sin causa confirmada. Si ocurre, revisar `docker compose logs --tail 50 homologation` y registrar el mensaje.
