# Auth Service: usuarios y roles

El Gateway es la entrada del frontend. La gestión de cuentas requiere JWT válido y rol `Administrador`; los roles admitidos son `Administrador` y `Profesor`. El usuario inicial de desarrollo se crea mediante la semilla local.

| Método y ruta | Descripción | Respuestas principales |
|---|---|---|
| `POST /auth/login` | Inicia sesión y entrega JWT y perfil | 200, 400, 401 |
| `GET /auth/me` | Consulta el perfil de la sesión | 200, 401 |
| `GET /auth/usuarios` | Lista cuentas y roles | 200, 401, 403 |
| `POST /auth/usuarios` | Crea una cuenta activa y le asigna rol | 201, 400, 401, 403, 409 |
| `PUT /auth/usuarios/{userId}` | Edita nombre, correo, rol y estado activo | 200, 400, 401, 403, 404, 409 |

El alta requiere correo válido, nombre completo, contraseña conforme a las políticas de Identity y un rol permitido. El correo debe ser único. La actualización permite desactivar o reactivar una cuenta; no se puede desactivar o degradar la propia cuenta de administrador ni retirar al último administrador activo.

## Ejemplo de alta

```json
{
  "email": "docente@uta.edu.ec",
  "nombreCompleto": "Nombre Apellido",
  "password": "Clave123",
  "rol": "Profesor"
}
```

## Ejemplo de actualización

```json
{
  "email": "docente@uta.edu.ec",
  "nombreCompleto": "Nombre Apellido",
  "rol": "Profesor",
  "activo": true
}
```
