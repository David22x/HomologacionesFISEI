# Revisión final de estabilización

Revisión realizada en la rama `fix/estabilizacion-flujo-homologacion`. Los resultados corresponden al código local; no acreditan que los contenedores desplegados ya incluyan estos cambios.

## Validaciones

| Comprobación | Resultado |
| --- | --- |
| Backend .NET 8 en Release y xUnit | 253 pruebas aprobadas, sin omisiones |
| Frontend TypeScript y compilación Vite | Correcto |
| Frontend Vitest | 29 pruebas aprobadas |
| ESLint | Sin errores ni advertencias |
| Docker Compose: validación de configuración | Correcto |
| Conflictos de formato en el diff | Sin errores |

Las suites cubren autenticación y roles, catálogo y publicación, registro e historial, importaciones ZIP/XLSX/PDF, datos externos incompletos, reglas y análisis IA, selección y decisiones docentes, estados, bloqueo de expedientes resueltos, auditoría, informes versionados e integridad del PDF y control de fallos de servicios. Las pruebas de IA con dependencias simuladas comprueban el contrato y la lógica; no constituyen una calibración académica del modelo con casos reales.

## Correcciones de esta revisión

- El login informa una caída temporal de Auth y conserva los campos escritos, en lugar de atribuir el fallo a las credenciales.
- Los selectores descartan un identificador anterior si el texto deja de coincidir con una opción. Un restablecimiento del formulario también limpia su etiqueta.
- La limpieza de filtros dependientes se realiza al cambiar su selección. Las consultas descartan respuestas que pertenecen a una selección anterior o a una pantalla cerrada.
- Se conserva la carga del catálogo al reabrir la edición del mismo estudiante y se verifica que pueda agregar otra materia.
- La vista de informes reinicia su estado al cambiar de expediente.
- Un fallo transitorio de base de datos al guardar un informe se propaga como indisponibilidad; no se confunde con una generación concurrente. Una prueba verifica la recuperación posterior.
- Se corrigieron los hallazgos de ESLint sin desactivar reglas.

## Alcance pendiente de verificar en despliegue

Las consultas locales a `/health/ready` de Gateway, Auth, Academic, Students, Documents, Homologation y Reports no obtuvieron una respuesta de disponibilidad. Por ello no se repitió el recorrido completo contra servicios desplegados, ni se comprobó un modelo Ollama real en esta revisión.

Antes de la presentación, reconstruir e iniciar los contenedores y ejecutar el recorrido descrito en [verificación del flujo](../verificacion-flujo-presentacion.md). Comprobar en pantalla un expediente desde su creación hasta la resolución y descarga del PDF, además de la recuperación de un servicio en un entorno de pruebas.

La nota mínima debe confirmarse con el docente antes de configurarla. La extracción de PDF escaneados requiere OCR previo. La calidad de las recomendaciones IA debe contrastarse con decisiones docentes de referencia.

La ejecución final del frontend se realizó con telemetría desactivada y proxy de salida cerrado, después de que la revisión automática bloqueara una comprobación por posible telemetría externa. No fue necesario autorizar envíos externos para completar las comprobaciones locales.
