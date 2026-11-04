const API_URL = (
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'
).replace(/\/$/, '');

export const estadosExpediente = ['Borrador', 'En revisión', 'Finalizado', 'Anulado'] as const;
export type EstadoExpediente = (typeof estadosExpediente)[number];

export interface Expediente {
  id: string;
  estudianteId: string;
  carreraDestinoId: string;
  mallaDestinoId: string;
  profesorId: string;
  profesorNombre: string;
  estado: number | string;
  creadoEn: string;
  actualizadoEn: string | null;
}

export interface ExpedienteCreateRequest {
  estudianteRefId: string;
  carreraDestinoRefId: string;
  mallaAplicableRefId: string;
}

async function request<T>(
  path: string,
  method: 'GET' | 'POST' | 'PUT' = 'GET',
  body?: unknown,
): Promise<T> {
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

  if (response.status === 401) throw new Error('Tu sesión expiró. Inicia sesión nuevamente.');
  if (response.status === 403) throw new Error('No tienes permiso para consultar expedientes.');
  if (!response.ok) {
    let detail = '';
    try {
      const problem = await response.json();
      detail = problem.detail || problem.title || problem.mensaje || problem.Mensaje || '';
    } catch {
      // Keep an HTTP status message for non-JSON error responses.
    }
    throw new Error(detail || `No se pudo completar la operación (HTTP ${response.status}).`);
  }
  return response.json() as Promise<T>;
}

export const expedientesService = {
  listar(): Promise<Expediente[]> {
    return request<Expediente[]>('/api/v1/homologacion/expedientes');
  },
  crear(datos: ExpedienteCreateRequest): Promise<Expediente> {
    return request<Expediente>('/api/v1/homologacion/expedientes', 'POST', datos);
  },
};
