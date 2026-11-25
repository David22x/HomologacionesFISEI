# Casos sintéticos de homologación

Carga completada: **6 estudiantes anteriores retirados del registro activo, 7 estudiantes DEMO activos y 7 expedientes creados**. Se conservó un respaldo local antes de retirar los anteriores. Los expedientes e informes históricos no se borraron.

Los registros `DEMO` son ficticios. Las carreras creadas son **DEMO Software - origen** y **DEMO TI - destino**. La asignatura destino es **FUNDAMENTOS DE COMPUTACION**, con 5 créditos, 100 horas y cuatro temas con sus subtemas: algoritmos, bases de datos, redes y pruebas de software.

| Estudiante | Caso | Qué mostrar |
| --- | --- | --- |
| Ana DEMO Directa | Una materia con los mismos contenidos y créditos | Coincidencia directa, resultado homologable y selección para revisión docente. |
| Bruno DEMO Compuesta | Dos materias de 2,5 créditos | Cada una se rechaza individualmente por créditos. En conjunto suman 5 créditos y cubren los cuatro temas. Analizar con IA y revisar la propuesta compuesta. |
| Carla DEMO Revision | Tres de los cuatro temas | Cobertura observada del 75 % frente al 80 % exigido. La IA conserva esta posibilidad cercana; el docente debe revisar y justificar. |
| Diego DEMO Creditos | Solo 2 créditos para un destino de 5 | Mostrar rechazo por créditos (40 %), aunque los contenidos coincidan. |
| Elena DEMO Contenidos | Economía y contabilidad frente a computación | Mostrar rechazo por contenidos. La IA no debe presentar una equivalencia académica sin evidencia. Los rechazos se consultan en el detalle/diagnóstico. |
| Fabian DEMO Externo | Historial externo con temas y subtemas | Comparar sin depender de una asignatura del catálogo local. |
| Gabriela DEMO Pendiente | Historial externo sin contenidos | Mostrar la información faltante; no permitir continuar con el análisis hasta completarla. |

Todos tienen nota 10 para que la demostración se centre en créditos y contenidos. La comparación semántica depende del modelo real; el porcentaje observado debe consultarse en la evaluación. Ninguna recomendación sustituye la decisión del docente.

Se verificó con los servicios reales: Ana obtuvo 100 %; Bruno obtuvo una propuesta compuesta de dos orígenes con 100 %; Carla obtuvo 75 %; Diego fue rechazado por 40 % de créditos; Elena tuvo 0 % de contenidos en la evaluación por reglas y ninguna equivalencia recomendada por la IA; Fabian obtuvo 100 %; Gabriela recibió HTTP 422 por información faltante. Los seis expedientes completos tienen análisis IA guardado. Gabriela conserva su expediente para demostrar el bloqueo.

## Recorrido sugerido

Ingresar como profesor y buscar `DEMO` en estudiantes. En expedientes, localizar al mismo estudiante y abrir su detalle. Los pares de comparación se preparan mediante el API. En el caso compuesto, pulsar **Analizar IA** para buscar la combinación y consultar el detalle de las dos materias de origen. Marcar las propuestas elegidas para comprobar su sincronización con la tabla definitiva.

Para resolver un caso, revisar la propuesta, registrar la decisión del profesor y su justificación cuando corresponda, resolver y generar el PDF. Los casos se dejan pendientes de decisión para poder practicar este recorrido sin registrar aprobaciones ficticias como si las hubiera tomado un docente.

## Preparación reproducible

Desde la raíz, con el Gateway y servicios iniciados y las credenciales locales de desarrollo configuradas en `infrastructure/.env`:

```powershell
# Solo consulta: no modifica registros.
python -B scripts/preparar_demostracion.py

# Crea los siete casos y evalúa sus pares.
python -B scripts/preparar_demostracion.py --aplicar --evaluar

# Deja también los resultados IA generados, sin tomar decisiones docentes.
python -B scripts/preparar_demostracion.py --aplicar --evaluar --analizar-ia

# Además retira del registro activo los estudiantes respaldados antes de la carga.
python -B scripts/preparar_demostracion.py --aplicar --evaluar --retirar-actuales
```

El script guarda un respaldo de los estudiantes anteriores y cada ID creado en `obj/demostracion`. No guarda credenciales. Esa carpeta está excluida de Git porque el respaldo puede contener datos personales. Reanudar el mismo comando utiliza los IDs guardados y evita repetir las creaciones completadas. Si se pierde la respuesta durante una escritura, comprobar su estado antes de reanudar.

La retirada se realiza solo después de crear los siete expedientes. Utiliza la eliminación lógica ya implementada: los registros dejan de aparecer entre los estudiantes activos, mientras que sus expedientes, auditoría e informes se conservan. No purga las bases de datos.
