import { afterEach, expect, it, vi } from 'vitest';
import { serviceFetch, ServiceUnavailableError } from './serviceFetch';
import { studentsService } from '../features/students/studentsService';
import { onSessionExpired } from '../features/auth/services/sessionEvents';

afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); vi.useRealTimers(); sessionStorage.clear(); });
it('una caida de Students no expira la sesion ni reintenta guardados', async () => {
  sessionStorage.setItem('accessToken', 'token'); const expired = vi.fn(); const unsubscribe = onSessionExpired(expired);
  const fetch = vi.fn().mockResolvedValue(new Response('', { status: 503 })); vi.stubGlobal('fetch', fetch);
  await expect(studentsService.guardarHistorial('id', {})).rejects.toThrow('consulta el estado');
  expect(fetch).toHaveBeenCalledOnce(); expect(expired).not.toHaveBeenCalled(); expect(sessionStorage.getItem('accessToken')).toBe('token'); unsubscribe();
});
it('un servicio recuperado se puede consultar de nuevo sin recargar la aplicacion', async () => {
  const fetch = vi.fn().mockRejectedValueOnce(new TypeError('Failed to fetch')).mockResolvedValueOnce(new Response('[]'));
  vi.stubGlobal('fetch', fetch);
  await expect(serviceFetch('/api/v1/catalogo/carreras')).rejects.toBeInstanceOf(ServiceUnavailableError);
  expect((await serviceFetch('/api/v1/catalogo/carreras')).ok).toBe(true);
});
it('el tiempo de espera libera la pantalla cuando el servicio no responde', async () => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', vi.fn((_input, options: RequestInit) => new Promise((_resolve, reject) => options.signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError'))))));
  const check = expect(serviceFetch('/api/v1/informes/test', {}, 10)).rejects.toThrow('tardó demasiado');
  await vi.advanceTimersByTimeAsync(11); await check;
});
it('una cancelacion voluntaria no se etiqueta como caida del servicio', async () => {
  const controller = new AbortController(); controller.abort();
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new DOMException('aborted', 'AbortError')));
  await expect(serviceFetch('/api/v1/catalogo', { signal: controller.signal })).rejects.toMatchObject({ name: 'AbortError' });
});
