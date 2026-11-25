export const AVAILABILITY_EVENT = 'fisei:availability';
export interface Availability { service: string; available: boolean; message?: string }
export class ServiceUnavailableError extends Error {
  readonly temporary = true;
  readonly service: string;
  constructor(message: string, service: string) { super(message); this.name = 'ServiceUnavailableError'; this.service = service; }
}
const names: Record<string, string> = { Auth: 'autenticación', Academic: 'catálogo académico', Students: 'estudiantes', Homologation: 'homologación', Documents: 'documentos', Reports: 'informes', AgentIA: 'Agent IA', Gateway: 'servidor' };
function serviceFor(path: string) {
  if (path.includes('/auth')) return 'Auth';
  if (path.includes('/catalogo')) return 'Academic';
  if (path.includes('/estudiantes')) return 'Students';
  if (path.includes('/homologacion')) return 'Homologation';
  if (path.includes('/documentos')) return 'Documents';
  if (path.includes('/informes')) return 'Reports';
  if (path.includes('/agent-ia')) return 'AgentIA';
  return 'Gateway';
}
export function publishAvailability(detail: Availability) { window.dispatchEvent(new CustomEvent<Availability>(AVAILABILITY_EVENT, { detail })); }
export async function serviceFetch(input: RequestInfo | URL, init: RequestInit = {}, timeout?: number): Promise<Response> {
  const path = input instanceof Request ? input.url : String(input);
  const service = serviceFor(path);
  const controller = new AbortController();
  const caller = init.signal;
  const abort = () => controller.abort();
  if (caller?.aborted) controller.abort(); else caller?.addEventListener('abort', abort, { once: true });
  const milliseconds = timeout ?? (path.includes('analizar-ia') ? 300000 : path.includes('/records/') ? 205000 : path.includes('/planes-analiticos/') ? 120000 : 35000);
  const timer = window.setTimeout(abort, milliseconds);
  try {
    const response = await fetch(input, { ...init, signal: controller.signal });
    if ((response.status >= 500 || response.status === 429)) throw new ServiceUnavailableError(`El servicio de ${names[service]} no está disponible temporalmente. Reintenta en unos segundos.`, service);
    publishAvailability({ service, available: true });
    return response;
  } catch (error) {
    if (caller?.aborted) throw error;
    const method = init.method?.toUpperCase() ?? (input instanceof Request ? input.method : 'GET');
    const readOnlyPost = /\/auth\/login(?:\?|$)|\/(?:records|planes-analiticos|plan-analitico)\/analizar(?:-registro)?(?:\?|$)|\/documentos\/importaciones(?:\?|$)/.test(path);
    const uncertain = !['GET', 'HEAD'].includes(method) && !readOnlyPost ? ' No se pudo confirmar el resultado: consulta el estado antes de repetir el guardado.' : '';
    const message = (error instanceof ServiceUnavailableError ? error.message : controller.signal.aborted
      ? `El servicio de ${names[service]} tardó demasiado en responder.` : `No se pudo conectar con el servicio de ${names[service]}. Comprueba tu conexión y reintenta.`) + uncertain;
    publishAvailability({ service, available: false, message });
    throw new ServiceUnavailableError(message, service);
  } finally {
    window.clearTimeout(timer);
    caller?.removeEventListener('abort', abort);
  }
}
