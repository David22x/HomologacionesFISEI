# Disponibilidad y recuperación de servicios

El sistema informa los fallos de las dependencias y permite utilizar las funciones que siguen disponibles. Una caída de SQL Server impide las operaciones que necesitan persistencia; una caída de Auth impide nuevos accesos y la validación inicial de una sesión. Estos casos no se presentan como operaciones completadas.

## Comportamiento implementado

| Fallo | Comportamiento |
| --- | --- |
| Servicio HTTP sin conexión | Respuesta controlada, aviso en la interfaz y consulta manual para comprobar recuperación. |
| Dependencia lenta | Tiempo máximo de espera; la interfaz libera la operación y ofrece reintentar. |
| Auth temporalmente inaccesible | Conserva el token y permite revalidarlo; no muestra información protegida antes de validarlo. Un 401 real mantiene el cierre de sesión. |
| Catálogo o Students no disponible al abrir expedientes | Conserva las consultas que sí respondieron. Si el detalle está incompleto, bloquea su edición para evitar guardar una selección basada en datos ausentes. |
| Fallo al analizar o guardar una importación | Informa del fallo y conserva la vista previa ya obtenida y las correcciones para reintentar. |
| SQL Server no disponible | Las operaciones dependientes fallan de forma controlada. Las consultas de disponibilidad comprueban la conexión real. |
| Agent IA u Ollama no disponible | Las operaciones que requieren IA informan el fallo; los demás módulos continúan. El respaldo de explicación por plantilla se mantiene donde ya estaba previsto. |
| Proceso que termina en Docker | `restart: unless-stopped` permite reiniciarlo. Docker no reinicia por sí solo un contenedor que sigue ejecutándose pero está marcado como `unhealthy`. |

El cliente HTTP común utiliza 30 segundos por defecto. Mantiene los presupuestos explícitos de las operaciones largas del agente y extracción documental. La interfaz espera 35 segundos para operaciones comunes y utiliza límites mayores para análisis e importaciones.

Los clientes HTTP internos incorporan un circuito por cliente y servidor: después de tres fallos de conexión o respuestas 5xx/429, rechazan nuevas llamadas durante 15 segundos. Pasado ese tiempo permiten comprobar la recuperación. Las comprobaciones de salud quedan fuera del circuito. La cancelación del usuario no cuenta como caída.

No se repiten automáticamente las escrituras. Si se pierde la respuesta de un guardado, el mensaje pide consultar el estado antes de repetirlo: el servidor podría haber completado la operación. La comprobación de disponibilidad tampoco vuelve a ejecutar la operación fallida.

## Supervisión

- `GET /health`: comprueba que el proceso responde.
- `GET /health/ready`: comprueba la base de datos en los servicios con persistencia. Agent IA comprueba Ollama y la presencia del modelo de embeddings configurado. No comprueba que todos los modelos generativos estén descargados.
- `GET /status/services` en el Gateway: requiere JWT y consulta en paralelo Auth, Academic, Students, Homologation, Documents, Reports y Agent IA; incluye también el Gateway. No devuelve direcciones internas ni detalles de excepciones.

La interfaz muestra un aviso al detectar un fallo y ofrece **Comprobar servicios**. Esta consulta actualiza el aviso; cada formulario conserva su acción explícita para recargar o reintentar. No hay sondeo continuo ni guardados automáticos.

## Activación y comprobación

Reconstruir los contenedores después de actualizar el código:

```powershell
docker compose -f infrastructure/docker-compose.yml up -d --build
```

En un entorno de pruebas con datos prescindibles, detener individualmente un servicio y comprobar: el módulo afectado informa del fallo, el resto mantiene la navegación, los datos introducidos permanecen y no se duplican escrituras. Volver a iniciarlo, esperar su disponibilidad y utilizar **Comprobar servicios** y el reintento del formulario. No detener SQL Server sobre una operación real para hacer esta comprobación.

Las pruebas automatizadas usan dependencias simuladas: verifican aislamiento del circuito, recuperación, ausencia de reintentos de POST, cancelación, tiempos de espera, respuestas sin datos sensibles, supervisión con un servicio caído, conservación de sesión y carga parcial de expedientes. No sustituyen una comprobación de infraestructura en el entorno de despliegue.
