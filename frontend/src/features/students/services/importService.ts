const API_URL = (
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'
).replace(/\/$/, '');

export interface ImportRowError {
  hoja: string;
  fila: number;
  columna: string;
  mensaje: string;
}

export interface ImportResult {
  mensaje: string;
  errores: ImportRowError[];
  vistaPrevia: ImportPreview | null;
}

export interface ImportPreview {
  cedula: string;
  tipoIdentificacion: number;
  nombres: string;
  apellidos: string;
  tipo: number;
  historial: {
    carreraOrigenNombre: string | null;
    institucionOrigen: string | null;
    nivelActual: number | null;
    asignaturas: Array<{
      codigo: string | null;
      nombre: string;
      creditos: number | null;
      horas: number | null;
      nota: number;
      periodo: string;
    }>;
  };
}

function authorizationHeaders(): HeadersInit {
  const token = sessionStorage.getItem('accessToken');
  if (!token) throw new Error('No hay una sesión activa. Inicia sesión nuevamente.');
  return { Authorization: `Bearer ${token}` };
}

export const importService = {
  async descargarPlantilla(): Promise<void> {
    const response = await fetch(`${API_URL}/api/v1/documentos/importaciones/plantilla`, {
      headers: authorizationHeaders(),
    });
    if (!response.ok) throw new Error('No se pudo descargar la plantilla. Verifica que el servicio de documentos esté disponible.');

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'plantilla_historial_externo.xlsx';
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  },

  async validarArchivo(file: File): Promise<ImportResult> {
    return sendFile('/api/v1/documentos/importaciones', file);
  },

  async confirmarArchivo(file: File, datos: ImportPreview): Promise<{ creado: boolean }> {
    const response = await sendFileResponse('/api/v1/documentos/importaciones/confirmar', file, datos);
    if (!response.ok) {
      const problem = await readJson(response);
      const message = typeof problem.detail === 'string'
        ? problem.detail
        : typeof problem.mensaje === 'string'
          ? problem.mensaje
          : 'No se pudo confirmar la importación.';
      throw new Error(message);
    }
    return { creado: response.status === 201 };
  },
};

async function sendFile(path: string, file: File): Promise<ImportResult> {
  const response = await sendFileResponse(path, file);
  const payload = await readJson(response);
  const result: ImportResult = {
    mensaje: typeof payload.mensaje === 'string' ? payload.mensaje : 'No se pudo validar el archivo.',
    errores: Array.isArray(payload.errores) ? payload.errores as ImportRowError[] : [],
    vistaPrevia: payload.vistaPrevia && typeof payload.vistaPrevia === 'object'
      ? payload.vistaPrevia as ImportPreview
      : null,
  };
  if (!response.ok && result.errores.length === 0) throw new Error(result.mensaje);
  if (response.ok && result.errores.length === 0 && !result.vistaPrevia)
    throw new Error('El servicio no devolvió los datos para la vista previa.');
  return result;
}

async function sendFileResponse(path: string, file: File, datos?: ImportPreview): Promise<Response> {
    const form = new FormData();
    form.append('archivo', file);
    if (datos) form.append('datos', JSON.stringify(datos));
    return fetch(`${API_URL}${path}`, {
      method: 'POST',
      headers: authorizationHeaders(),
      body: form,
    });
}

async function readJson(response: Response): Promise<Record<string, unknown>> {
  try {
    const data: unknown = await response.json();
    return typeof data === 'object' && data !== null
      ? data as Record<string, unknown>
      : {};
  } catch {
    throw new Error('El servicio devolvió una respuesta que no se pudo leer.');
  }
}
