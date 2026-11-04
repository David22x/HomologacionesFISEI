
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

export interface Tema {
  id: string;
  asignaturaId: string;
  nombre: string;
  orden: number;
  horasEstimadas: number | null;
}

export interface Subtema {
  id: string;
  temaId: string;
  nombre: string;
  orden: number;
  horasEstimadas: number | null;
}

export interface TemaWrite {
  nombre: string;
  orden: number;
  horasEstimadas: number | null;
}

export interface PlanAnaliticoPreview {
  nombre: string; codigo: string; creditos: number; horas: number;
  unidadOrganizacional: string; temas: { nombre: string; subtemas: string[] }[]; advertencias: string[];
}

async function cargarPlan(asignaturaId: string, archivo: File, confirmar?: PlanAnaliticoPreview): Promise<PlanAnaliticoPreview | void> {
  const token = sessionStorage.getItem('accessToken');
  if (!token) throw new Error('No hay una sesión activa. Inicia sesión nuevamente.');
  const body = new FormData(); body.append('archivo', archivo);
  const ruta = `${API_URL}/api/v1/catalogo/asignaturas/${asignaturaId}/plan-analitico/${confirmar ? 'confirmar' : 'analizar'}`;
  if (confirmar) body.append('datos', JSON.stringify(confirmar));
  const response = await fetch(ruta, { method: 'POST', headers: { Authorization: `Bearer ${token}` }, body });
  if (!response.ok) { let message = `No se pudo procesar el PDF (HTTP ${response.status}).`; try { const err = await response.json(); message = err.detail || err.title || message; } catch { /* mensaje HTTP */ } throw new Error(message); }
  if (confirmar) return;
  return response.json() as Promise<PlanAnaliticoPreview>;
}

async function descargarPlan(asignaturaId: string): Promise<void> {
  const token = sessionStorage.getItem('accessToken');
  const response = await fetch(`${API_URL}/api/v1/catalogo/asignaturas/${asignaturaId}/plan-analitico/archivo`, { headers: { Authorization: `Bearer ${token ?? ''}` } });
  if (!response.ok) { let message = 'No se pudo descargar el plan analítico.'; try { const err = await response.json(); message = err.detail || err.title || message; } catch { /* mensaje genérico */ } throw new Error(message); }
  const blob = await response.blob(); const url = URL.createObjectURL(blob); const link = document.createElement('a');
  link.href = url; link.download = response.headers.get('content-disposition')?.match(/filename="?([^";]+)"?/i)?.[1] ?? 'plan-analitico.pdf'; link.click(); URL.revokeObjectURL(url);
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
  analizarPlan(asignaturaId: string, archivo: File) { return cargarPlan(asignaturaId, archivo) as Promise<PlanAnaliticoPreview>; },
  confirmarPlan(asignaturaId: string, archivo: File, datos: PlanAnaliticoPreview) { return cargarPlan(asignaturaId, archivo, datos) as Promise<void>; },
  descargarPlan,
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

  obtenerTemas(asignaturaId: string): Promise<Tema[]> {
    return solicitar<Tema[]>(`/api/v1/catalogo/asignaturas/${asignaturaId}/temas`);
  },

  crearTema(asignaturaId: string, datos: TemaWrite): Promise<Tema> {
    return solicitarConMetodo<Tema>(`/api/v1/catalogo/asignaturas/${asignaturaId}/temas`, 'POST', datos);
  },

  actualizarTema(temaId: string, datos: TemaWrite): Promise<Tema> {
    return solicitarConMetodo<Tema>(`/api/v1/catalogo/temas/${temaId}`, 'PUT', datos);
  },

  eliminarTema(temaId: string): Promise<void> {
    return solicitarConMetodo<void>(`/api/v1/catalogo/temas/${temaId}`, 'DELETE');
  },

  obtenerSubtemas(temaId: string): Promise<Subtema[]> {
    return solicitar<Subtema[]>(`/api/v1/catalogo/temas/${temaId}/subtemas`);
  },

  crearSubtema(temaId: string, datos: TemaWrite): Promise<Subtema> {
    return solicitarConMetodo<Subtema>(`/api/v1/catalogo/temas/${temaId}/subtemas`, 'POST', datos);
  },

  actualizarSubtema(subtemaId: string, datos: TemaWrite): Promise<Subtema> {
    return solicitarConMetodo<Subtema>(`/api/v1/catalogo/subtemas/${subtemaId}`, 'PUT', datos);
  },

  eliminarSubtema(subtemaId: string): Promise<void> {
    return solicitarConMetodo<void>(`/api/v1/catalogo/subtemas/${subtemaId}`, 'DELETE');
  },
};
