import { notifySessionExpired } from '../../auth/services/sessionEvents';

export async function uploadImport<T>(path: string, file: File, data?: unknown): Promise<T> {
  const token = sessionStorage.getItem('accessToken');
  if (!token) throw new Error('Inicia sesión para importar archivos.');
  const body = new FormData(); body.append('archivo', file);
  if (data !== undefined) body.append('datos', JSON.stringify(data));
  const response = await fetch(`${(import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080').replace(/\/$/, '')}${path}`, { method: 'POST', headers: { Authorization: `Bearer ${token}` }, body });
  if (response.status === 401) notifySessionExpired();
  const result = await response.json().catch(() => null);
  if (!response.ok) {
    const fields = Object.entries(result?.errors ?? {}).map(([name, errors]) => `${name}: ${Array.isArray(errors) ? errors.join(' ') : String(errors)}`).join('\n');
    throw new Error(result?.mensaje || result?.detail || fields || (typeof result === 'string' ? result : `No se pudo completar la importación (${response.status}).`));
  }
  return result as T;
}
