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
    const form = new FormData();
    form.append('archivo', file);
    const response = await fetch(`${API_URL}/api/v1/documentos/importaciones`, {
      method: 'POST',
      headers: authorizationHeaders(),
      body: form,
    });

    let payload: Partial<ImportResult>;
    try {
      payload = await response.json() as Partial<ImportResult>;
    } catch {
      throw new Error('El servicio devolvió una respuesta que no se pudo leer.');
    }
    const result: ImportResult = {
      mensaje: payload.mensaje || 'No se pudo validar el archivo.',
      errores: Array.isArray(payload.errores) ? payload.errores : [],
    };
    if (!response.ok && result.errores.length === 0) throw new Error(result.mensaje);
    return result;
  },
};
