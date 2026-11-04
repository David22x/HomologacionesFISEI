
const API_URL = (
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'
).replace(/\/$/, '');

export interface AuthUser {
  id: string;
  email: string;
  nombreCompleto: string;
  roles: string[];
}

export interface ManagedUser extends AuthUser {
  activo: boolean;
}

export interface CreateManagedUser {
  email: string;
  nombreCompleto: string;
  password: string;
  rol: 'Administrador' | 'Profesor';
}

export interface UpdateManagedUser {
  email: string;
  nombreCompleto: string;
  rol: 'Administrador' | 'Profesor';
  activo: boolean;
}

interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: AuthUser;
}

export async function login(
  email: string,
  password: string
): Promise<LoginResponse> {
  const response = await fetch(`${API_URL}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });

  if (!response.ok) {
    throw new Error('LOGIN_FAILED');
  }

  const data: unknown = await response.json();

  if (
    typeof data !== 'object' ||
    data === null ||
    !('accessToken' in data) ||
    typeof data.accessToken !== 'string' ||
    !data.accessToken ||
    !('user' in data) ||
    typeof data.user !== 'object' ||
    data.user === null ||
    !('id' in data.user) ||
    typeof data.user.id !== 'string' ||
    !('email' in data.user) ||
    typeof data.user.email !== 'string' ||
    !('nombreCompleto' in data.user) ||
    typeof data.user.nombreCompleto !== 'string' ||
    !('roles' in data.user) ||
    !Array.isArray(data.user.roles) ||
    !data.user.roles.every((role: unknown) => typeof role === 'string')
  ) {
    throw new Error('INVALID_LOGIN_RESPONSE');
  }

  return data as LoginResponse;
}

export async function getCurrentUser(
  token: string
): Promise<AuthUser> {
  const response = await fetch(`${API_URL}/auth/me`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    throw new Error('SESSION_INVALID');
  }

  return response.json() as Promise<AuthUser>;
}

export const userManagementService = {
  async listar(): Promise<ManagedUser[]> {
    return requestUsers<ManagedUser[]>('/auth/usuarios');
  },
  async crear(data: CreateManagedUser): Promise<ManagedUser> {
    return requestUsers<ManagedUser>('/auth/usuarios', 'POST', data);
  },
  async actualizar(id: string, data: UpdateManagedUser): Promise<ManagedUser> {
    return requestUsers<ManagedUser>(`/auth/usuarios/${id}`, 'PUT', data);
  },
};

async function requestUsers<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const token = sessionStorage.getItem('accessToken');
  if (!token) throw new Error('No hay una sesión activa. Inicia sesión nuevamente.');
  const response = await fetch(`${API_URL}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!response.ok) {
    let message = response.status === 403 ? 'Solo un administrador puede gestionar usuarios.' : 'No se pudo guardar el usuario.';
    try {
      const payload = await response.json() as { message?: string; detail?: string; errors?: string[] };
      message = payload.message ?? payload.detail ?? payload.errors?.join(' ') ?? message;
    } catch { /* Keep a safe generic message for non-JSON responses. */ }
    throw new Error(message);
  }
  return response.json() as Promise<T>;
}
