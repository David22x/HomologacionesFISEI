# Homologation Service: expedientes

Los endpoints se consumen a través del API Gateway y requieren JWT. El profesor crea expedientes; Administrador y Profesor pueden consultarlos. Cada profesor ve sus expedientes y Administrador puede ver todos.

| Método y ruta | Descripción | Respuestas principales |
|---|---|---|
| `POST /api/v1/homologacion/expedientes` | Crea expediente en estado `Borrador` | 201, 400, 401, 403, 404, 503 |
| `GET /api/v1/homologacion/expedientes` | Lista expedientes visibles para el rol | 200, 401, 403 |

Antes de guardar, Homologation consulta Students Service para verificar el estudiante y Academic Service para verificar que la malla pertenezca a la carrera indicada. Las referencias entre servicios son identificadores lógicos; no hay claves foráneas entre bases de datos. Si un servicio dependiente no está disponible, la creación se rechaza y no se guarda el expediente.

La evaluación por créditos, las propuestas de equivalencia, la decisión académica y el informe quedan para las historias posteriores del Sprint 3.
