
const API_URL = (
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'
).replace(/\/$/, '');

export interface AuthUser {
  id: string;
  email: string;
  nombreCompleto: string;
  roles: string[];
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
