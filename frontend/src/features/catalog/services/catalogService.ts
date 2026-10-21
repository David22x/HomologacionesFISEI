
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
  const token = sessionStorage.getItem('accessToken');

  if (!token) {
    throw new Error('No hay una sesión activa. Inicia sesión nuevamente.');
  }

  const response = await fetch(`${API_URL}${ruta}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  if (response.status === 401) {
    throw new Error('Tu sesión expiró. Inicia sesión nuevamente.');
  }

  if (response.status === 403) {
    throw new Error('No tienes permiso para consultar el catálogo.');
  }

  if (!response.ok) {
    throw new Error(
      `No se pudo consultar el catálogo (HTTP ${response.status}).`,
    );
  }

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
};
