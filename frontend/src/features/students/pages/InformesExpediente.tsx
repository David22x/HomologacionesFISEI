import { useEffect, useState } from 'react';
import { expedientesService, type VersionInforme } from '../expedientesService';

interface InformesExpedienteProps {
  expedienteId: string;
  puedeGenerar: boolean;
}

export default function InformesExpediente({ expedienteId, puedeGenerar }: InformesExpedienteProps) {
  const [versiones, setVersiones] = useState<VersionInforme[]>([]);
  const [cargando, setCargando] = useState(true);
  const [generando, setGenerando] = useState(false);
  const [descargando, setDescargando] = useState<number | null>(null);
  const [error, setError] = useState('');
  const [aviso, setAviso] = useState('');

  useEffect(() => {
    let vigente = true;
    expedientesService.listarInformes(expedienteId)
      .then(lista => { if (vigente) setVersiones(lista); })
      .catch(cause => { if (vigente) setError(cause instanceof Error ? cause.message : 'No se pudieron consultar los informes.'); })
      .finally(() => { if (vigente) setCargando(false); });
    return () => { vigente = false; };
  }, [expedienteId]);

  async function generar() {
    setGenerando(true);
    setError('');
    setAviso('');
    try {
      const antes = versiones.length;
      const informe = await expedientesService.generarInforme(expedienteId);
      const lista = await expedientesService.listarInformes(expedienteId);
      setVersiones(lista);
      setAviso(lista.length > antes
        ? `Se generó la versión ${informe.version} del informe.`
        : `El expediente no cambió desde la versión ${informe.version}; no se creó una versión nueva.`);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo generar el informe.');
    } finally {
      setGenerando(false);
    }
  }

  async function descargar(version: number) {
    setDescargando(version);
    setError('');
    try {
      const pdf = await expedientesService.descargarInforme(expedienteId, version);
      const url = URL.createObjectURL(pdf);
      const enlace = document.createElement('a');
      enlace.href = url;
      enlace.download = `homologacion-${expedienteId}-v${version}.pdf`;
      try {
        document.body.append(enlace);
        enlace.click();
      } finally {
        enlace.remove();
        // Permite que el navegador inicie la descarga antes de liberar el Blob.
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo descargar el informe.');
    } finally {
      setDescargando(null);
    }
  }

  return <section className="informes-expediente" aria-labelledby="informes-heading">
    <h4 id="informes-heading">Informe PDF</h4>
    {puedeGenerar && <button type="button" className="students-primary" disabled={generando} onClick={() => { void generar(); }}>
      {generando ? 'Generando…' : 'Generar informe'}
    </button>}
    {error && <p className="ia-error" role="alert">{error}</p>}
    {aviso && <p className="catalog-message" role="status">{aviso}</p>}
    {cargando
      ? <p className="catalog-message" role="status">Consultando informes…</p>
      : versiones.length === 0
        ? <p className="catalog-message">Aún no hay informes generados para este expediente.</p>
        : <ul className="informes-versiones" aria-label="Versiones del informe">{versiones.map(item => <li key={item.id}>
          <span>Versión {item.version} · {new Date(item.generadoEn).toLocaleString()}</span>
          <button type="button" className="students-secondary" disabled={descargando === item.version}
            onClick={() => { void descargar(item.version); }}>
            {descargando === item.version ? 'Descargando…' : `Descargar versión ${item.version}`}
          </button>
        </li>)}</ul>}
  </section>;
}
