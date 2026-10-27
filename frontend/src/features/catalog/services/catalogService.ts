
const API_URL = (
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'
).replace(/\/$/, '');

export interface Carrera {
  id: string;
  codigo: string;
  nombre: string;
  esDemo: boolean;
}

export interface MallaCurricular {
  id: string;
  carreraId: string;
  version: string;
  vigente: boolean;
  estado?: string;
}

export interface Nivel {
  id: string;
  mallaVersionId: string;
  numero: number;
}

export interface Asignatura {
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

async function solicitar<T>(ruta: string): Promise<T> {
  return solicitarConMetodo<T>(ruta, 'GET');
}

async function solicitarConMetodo<T>(
  ruta: string,
  metodo: string,
  cuerpo?: unknown,
): Promise<T> {
  const token = sessionStorage.getItem('accessToken');

  if (!token) {
    throw new Error('No hay una sesión activa. Inicia sesión nuevamente.');
  }

  const response = await fetch(`${API_URL}${ruta}`, {
    method: metodo,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(cuerpo === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    ...(cuerpo === undefined ? {} : { body: JSON.stringify(cuerpo) }),
  });

  if (response.status === 401) {
    throw new Error('Tu sesión expiró. Inicia sesión nuevamente.');
  }

  if (response.status === 403) {
    throw new Error('No tienes permiso para consultar el catálogo.');
  }

  if (!response.ok) {
    let detalle = '';
    try {
      const error = await response.json();
      detalle = error.detail || error.title || '';
    } catch {
      // Keep a useful status message when the server returns no JSON body.
    }
    throw new Error(detalle || `No se pudo guardar el catálogo (HTTP ${response.status}).`);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const catalogService = {
  obtenerCarreras(): Promise<Carrera[]> {
    return solicitar<Carrera[]>('/api/v1/catalogo/carreras');
  },

  obtenerMallas(carreraId: string): Promise<MallaCurricular[]> {
    return solicitar<MallaCurricular[]>(
      `/api/v1/catalogo/carreras/${carreraId}/mallas`,
    );
  },

  obtenerNiveles(mallaId: string): Promise<Nivel[]> {
    return solicitar<Nivel[]>(
      `/api/v1/catalogo/mallas/${mallaId}/niveles`,
    );
  },

  obtenerAsignaturas(
    mallaId: string,
    nivelId: string,
    busqueda: string,
  ): Promise<Asignatura[]> {
    const parametros = new URLSearchParams({
      nivelId,
    });

    if (busqueda.trim()) {
      parametros.set('search', busqueda.trim());
    }

    return solicitar<Asignatura[]>(
      `/api/v1/catalogo/mallas/${mallaId}/asignaturas?${parametros.toString()}`,
    );
  },

  guardarCarrera(id: string | undefined, datos: Omit<Carrera, 'id'>): Promise<Carrera> {
    return solicitarConMetodo(id ? `/api/v1/catalogo/carreras/${id}` : '/api/v1/catalogo/carreras', id ? 'PUT' : 'POST', datos);
  },

  guardarMalla(id: string | undefined, carreraId: string, version: string): Promise<MallaCurricular> {
    return solicitarConMetodo(id ? `/api/v1/catalogo/mallas/${id}` : `/api/v1/catalogo/carreras/${carreraId}/mallas`, id ? 'PUT' : 'POST', { version });
  },

  publicarMalla(id: string): Promise<MallaCurricular> {
    return solicitarConMetodo(`/api/v1/catalogo/mallas/${id}/publicar`, 'POST');
  },

  guardarNivel(id: string | undefined, mallaId: string, numero: number): Promise<Nivel> {
    return solicitarConMetodo(id ? `/api/v1/catalogo/niveles/${id}` : `/api/v1/catalogo/mallas/${mallaId}/niveles`, id ? 'PUT' : 'POST', { numero });
  },

  guardarAsignatura(id: string | undefined, mallaId: string, datos: Omit<Asignatura, 'id' | 'mallaVersionId' | 'nivelNumero'>): Promise<Asignatura> {
    return solicitarConMetodo(id ? `/api/v1/catalogo/asignaturas/${id}` : `/api/v1/catalogo/mallas/${mallaId}/asignaturas`, id ? 'PUT' : 'POST', datos);
  },
};
