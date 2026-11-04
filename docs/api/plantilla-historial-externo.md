# Plantilla XLSX del historial de un estudiante externo

Contrato de la plantilla de la HU-S2-09 (Sprint 2). Archivo: `plantilla_historial_externo.xlsx`.
Lo genera `scripts/generar_plantilla_xlsx.py`; Documents Service lo sirve para descargar y lo valida al cargarlo.

## Endpoints

- `GET /api/v1/documentos/importaciones/plantilla`: descarga la plantilla `.xlsx`.
- `POST /api/v1/documentos/importaciones`: recibe el archivo multipart con el campo `archivo` y valida el libro sin guardar datos.
- `POST /api/v1/documentos/importaciones/confirmar`: recibe otra vez el archivo multipart (`archivo`), lo valida y confirma la importación en Students Service.

La respuesta de validación incluye `mensaje`, `errores` y `vistaPrevia`. Cada elemento de `errores` informa `hoja`, `fila`, `columna` y `mensaje`; si el libro es válido, `vistaPrevia` contiene el estudiante y su historial normalizado para revisión de solo lectura. Si hay errores, no se devuelve una vista previa ni se puede confirmar. El Gateway publica las tres rutas y requiere una sesión autenticada.

La confirmación vuelve a validar el archivo y envía el estudiante externo y sus asignaturas a `PUT /internal/estudiantes/importaciones` de Students, reenviando el JWT del usuario. Devuelve `201` al crear el estudiante o `200` si ya existía o si la misma importación se confirma otra vez. Una identificación existente de estudiante interno produce `409`. Las asignaturas de la confirmación reemplazan las importadas previamente, mientras que repetir el mismo contenido conserva los identificadores y la fecha del historial.

La ruta `/internal` no se publica por el Gateway. Students exige JWT y rol `Administrador` o `Profesor`; Documents llama directamente a la dirección configurada en `StudentsService:BaseUrl` (por defecto `http://localhost:5194`). La hoja `Contenidos` se valida, pero no se persiste.

## Reglas del archivo

- Solo se acepta `.xlsx` de hasta 5 MB. Se rechazan `.xlsm` y cualquier otro formato.
- Deben existir las tres hojas, con estos nombres exactos: `Estudiante`, `Asignaturas` y `Contenidos`.
- La primera fila de cada hoja es el encabezado. Los nombres de columna deben ser exactos; el orden de las columnas no importa.
- Las filas completamente vacias se ignoran.
- Cada error se informa con **hoja, fila (numero de fila de Excel), columna y mensaje**. Mientras haya errores no se puede confirmar la importacion.

## Hoja `Estudiante` (exactamente una fila de datos)

| Columna | Obligatoria | Regla |
|---|---|---|
| Identificacion | Si | Si `TipoIdentificacion` es Cedula: 10 digitos con digito verificador. Si es Pasaporte: 5 a 20 letras o numeros, sin simbolos |
| TipoIdentificacion | Si | `Cedula` o `Pasaporte` |
| Nombres | Si | Texto, maximo 150 caracteres |
| Apellidos | Si | Texto, maximo 150 caracteres |
| InstitucionOrigen | Si | Texto, maximo 200 caracteres |
| CarreraOrigen | Si | Texto, maximo 200 caracteres |
| NivelActual | No | Entero mayor que 0 |

## Hoja `Asignaturas` (una fila por asignatura aprobada, al menos una)

| Columna | Obligatoria | Regla |
|---|---|---|
| Codigo | Si | Texto, maximo 30 caracteres. No se puede repetir en la hoja |
| Nombre | Si | Texto, maximo 200 caracteres |
| Creditos | Si | Numero mayor que 0 |
| Horas | Si | Entero mayor que 0 |
| Nota | Si | Numero entre 0 y 10 |
| Periodo | Si | Texto, maximo 30 caracteres |

## Hoja `Contenidos` (temas por asignatura)

| Columna | Obligatoria | Regla |
|---|---|---|
| CodigoAsignatura | Si | Debe existir en la columna `Codigo` de la hoja `Asignaturas` |
| Tema | Si | Texto, maximo 300 caracteres |
| Subtema | No | Texto, maximo 300 caracteres |

No se puede repetir la combinacion `CodigoAsignatura` + `Tema` + `Subtema`.

## Correspondencia con StudentsDB

- `Estudiante` -> `Estudiantes` (`Identificacion` -> `Cedula`, `TipoIdentificacion`).
- `Estudiante.InstitucionOrigen`, `CarreraOrigen`, `NivelActual` -> `HistorialesAcademicos`.
- `Asignaturas` -> `AsignaturasCursadas` (`Codigo`, `Nombre`, `Creditos`, `Horas`, `Nota`, `Periodo`).
- `Contenidos`: se valida, pero no se guarda en esta versión.

## Supuestos a confirmar con el equipo

- Un archivo trae un solo estudiante.
- La nota va de 0 a 10.
