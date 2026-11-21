import { notifySessionExpired } from '../auth/services/sessionEvents';

const API_URL = (
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'
).replace(/\/$/, '');

export const estadosExpediente = ['Borrador', 'En revisión', 'Finalizado', 'Anulado', 'Analizado con IA', 'Resuelto'] as const;
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

export interface ParHomologacion {
  id: string;
  expedienteId: string;
  asignaturaCursadaId: string;
  asignaturaDestinoId: string;
  precargado: boolean;
}

export interface ResultadoReglasExpediente {
  resultado: string | number;
  razonCreditos: number | null;
  coberturaContenidos: number | null;
  razonHoras: number | null;
  advertencias: string[];
  versionReglas: string;
}

export interface ResultadoParExpediente {
  parId: string;
  asignaturaCursadaId: string;
  nombreOrigen: string;
  asignaturaDestinoId: string;
  nombreDestino: string;
  evaluacion: { reglas: ResultadoReglasExpediente };
}

export interface EjecucionReglasExpediente {
  id: string;
  ejecutadoEn: string;
  estado: string;
  resultados: ResultadoParExpediente[];
}

export interface TemaPropuestaIa {
  codigoDestino: string;
  descripcion: string;
  horas: number | null;
  similitud: number | null;
  codigoOrigen: string | null;
  evidencia: string | null;
}

export interface PropuestaExpedienteIa {
  asignaturaDestinoId: string;
  nombreDestino: string;
  origenes: Array<{ asignaturaCursadaId: string; nombre: string }>;
  revisionesDocente?: RevisionDocentePropuesta[] | null;
  evaluacion: {
    explicacion?: string;
    fuenteExplicacion?: string;
    reglas: ResultadoReglasExpediente;
    cobertura: {
      porcentaje: number | null;
      temasCoincidentes: TemaPropuestaIa[];
      temasAusentes: TemaPropuestaIa[];
    };
  };
}

export interface RevisionDocentePropuesta {
  tipo: 'AceptaPropuestaIa' | 'ConclusionPropia';
  resultadoFinal: string | number;
  observacion: string | null;
  revisadoEn: string;
  profesorId: string;
  profesorNombre: string;
}

export interface RevisionPropuestaIaRequest {
  asignaturaDestinoId: string;
  origenIds: string[];
  aceptarPropuestaIa: boolean;
  resultadoPropio: number | null;
  observacion: string | null;
}

export interface AnalisisExpedienteIa {
  id: string;
  ejecutadoEn: string;
  estado: string;
  propuestas: PropuestaExpedienteIa[];
  advertencias: string[];
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

  if (response.status === 401) {
    notifySessionExpired();
    throw new Error('Tu sesión expiró. Inicia sesión nuevamente.');
  }
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
  if (response.status === 204) return null as T;
  return response.json() as Promise<T>;
}

export const expedientesService = {
  consultarAnalisis(expedienteId: string): Promise<AnalisisExpedienteIa | null> {
    return request<AnalisisExpedienteIa | null>(
      '/api/v1/homologacion/expedientes/' + expedienteId + '/analisis-ia');
  },
  listar(): Promise<Expediente[]> {
    return request<Expediente[]>('/api/v1/homologacion/expedientes');
  },
  crear(datos: ExpedienteCreateRequest): Promise<Expediente> {
    return request<Expediente>('/api/v1/homologacion/expedientes', 'POST', datos);
  },
  listarPares(expedienteId: string): Promise<ParHomologacion[]> {
    return request<ParHomologacion[]>(`/api/v1/homologacion/expedientes/${expedienteId}/pares`);
  },
  guardarPares(expedienteId: string, pares: Array<Pick<ParHomologacion, 'asignaturaCursadaId' | 'asignaturaDestinoId'>>): Promise<ParHomologacion[]> {
    return request<ParHomologacion[]>(`/api/v1/homologacion/expedientes/${expedienteId}/pares`, 'PUT', pares);
  },
  precargarPares(expedienteId: string): Promise<ParHomologacion[]> {
    return request<ParHomologacion[]>(`/api/v1/homologacion/expedientes/${expedienteId}/pares/precargar`, 'POST');
  },
  evaluarPorReglas(expedienteId: string): Promise<EjecucionReglasExpediente> {
    return request<EjecucionReglasExpediente>(`/api/v1/homologacion/expedientes/${expedienteId}/evaluar-reglas`, 'POST');
  },
  analizarConIa(expedienteId: string): Promise<AnalisisExpedienteIa> {
    return request<AnalisisExpedienteIa>(`/api/v1/homologacion/expedientes/${expedienteId}/analizar-ia`, 'POST');
  },
  revisarPropuestaIa(expedienteId: string, datos: RevisionPropuestaIaRequest): Promise<RevisionDocentePropuesta> {
    return request<RevisionDocentePropuesta>(
      `/api/v1/homologacion/expedientes/${expedienteId}/analisis-ia/propuestas/revision`, 'PUT', datos,
    );
  },
};
