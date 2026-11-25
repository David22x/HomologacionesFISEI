import { useEffect, useState } from 'react';
import { AVAILABILITY_EVENT, publishAvailability, serviceFetch, type Availability } from '../lib/serviceFetch';

export default function ServiceAvailability() {
  const [failures, setFailures] = useState<Record<string, string>>({});
  const [checking, setChecking] = useState(false);
  useEffect(() => {
    const listener = (event: Event) => {
      const status = (event as CustomEvent<Availability>).detail;
      setFailures(current => {
        const next = { ...current };
        if (status.available) delete next[status.service]; else next[status.service] = status.message ?? `${status.service} no está disponible.`;
        return next;
      });
    };
    window.addEventListener(AVAILABILITY_EVENT, listener);
    return () => window.removeEventListener(AVAILABILITY_EVENT, listener);
  }, []);
  async function check() {
    setChecking(true);
    try {
      const base = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080').replace(/\/$/, '');
      const response = await serviceFetch(base + '/status/services', { headers: { Authorization: `Bearer ${sessionStorage.getItem('accessToken') ?? ''}` } }, 8000);
      if (!response.ok) return;
      const status = await response.json() as { services: { service: string; available: boolean }[] };
      for (const service of status.services) publishAvailability({ ...service, message: `${service.service} no está disponible temporalmente. Las funciones que dependen de este servicio deben esperar a su recuperación.` });
    } catch { /* El adaptador común ya informa la indisponibilidad. */ }
    finally { setChecking(false); }
  }
  if (!Object.keys(failures).length) return null;
  return <aside className="service-availability" aria-label="Disponibilidad de servicios" role="status">
    <div><strong>Algunas funciones no están disponibles temporalmente</strong><p>Puedes seguir utilizando los módulos disponibles. No se repetirán guardados automáticamente.</p>
      <details><summary>Ver servicios afectados ({Object.keys(failures).length})</summary><ul>{Object.entries(failures).map(([key, message]) => <li key={key}>{message}</li>)}</ul></details>
    </div><button type="button" className="students-secondary" disabled={checking} onClick={() => { void check(); }}>{checking ? 'Comprobando…' : 'Comprobar servicios'}</button>
  </aside>;
}
