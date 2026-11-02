const API_URL = (
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'
).replace(/\/$/, '');

export interface AsignaturaCatalogo {
  id: string;
  mallaVersionId: string;
  nivelId: string;
  nivelNumero: number;
  codigo: string;
  nombre: string;
  creditos: number;
  horas: number;
  unidadOrganizacional: string | null;
}

export interface AsignaturaCursada {
  id?: string;
  asignaturaId: string;
  codigo: string | null;
  nombre: string;
  creditos: number | null;
  horas: number | null;
  nota: number;
  periodo: string;
}

export interface HistorialAcademico {
  id: string;
  estudianteId: string;
  carreraOrigenId: string | null;
  carreraOrigenNombre: string | null;
  institucionOrigen: string | null;
  nivelActual: number | null;
  actualizadoEn: string;
  asignaturas: AsignaturaCursada[];
}

export interface Estudiante {
  id: string;
  cedula: string;
  tipoIdentificacion: number;
  nombres: string;
  apellidos: string;
  tipo: number;
  creadoPor: string | null;
  creadoEn: string;
  modificadoPor: string | null;
  modificadoEn: string;
  historial: HistorialAcademico | null;
}

export interface EstudianteWriteRequest {
  cedula: string;
  tipoIdentificacion: 0;
  nombres: string;
  apellidos: string;
  tipo: 0;
  historial: {
    carreraOrigenId: string;
    carreraOrigenNombre: null;
    institucionOrigen: null;
    nivelActual: number;
    asignaturas: Omit<AsignaturaCursada, 'id'>[];
  };
}

async function request<T>(path: string, method: 'GET' | 'POST' | 'PUT' = 'GET', body?: unknown): Promise<T> {
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
  if (response.status === 403) throw new Error('No tienes permiso para administrar estudiantes.');
  if (!response.ok) {
    let detail = '';
    try {
      const problem = await response.json();
      detail = problem.detail || problem.title || '';
    } catch {
      // Keep an HTTP status message for non-JSON error responses.
    }
    throw new Error(detail || `No se pudo completar la operación (HTTP ${response.status}).`);
  }
  return response.json() as Promise<T>;
}

export const studentsService = {
  listar(): Promise<Estudiante[]> {
    return request<Estudiante[]>('/api/v1/estudiantes');
  },
  crear(datos: EstudianteWriteRequest): Promise<Estudiante> {
    return request<Estudiante>('/api/v1/estudiantes', 'POST', datos);
  },
  actualizar(id: string, datos: EstudianteWriteRequest): Promise<Estudiante> {
    return request<Estudiante>(`/api/v1/estudiantes/${id}`, 'PUT', datos);
  },
};
