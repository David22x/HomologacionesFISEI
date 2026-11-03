import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { importService, type ImportResult } from '../services/importService';

const MAX_FILE_SIZE = 5 * 1024 * 1024;

export default function ImportHistoryPage() {
  const fileInput = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [downloading, setDownloading] = useState(false);

  function chooseFile(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0] ?? null;
    setFile(selected);
    setResult(null);
    setError('');
    if (selected && !selected.name.toLowerCase().endsWith('.xlsx')) {
      setError('Selecciona un archivo con formato .xlsx.');
    } else if (selected && selected.size > MAX_FILE_SIZE) {
      setError('El archivo supera el límite de 5 MB.');
    }
  }

  async function downloadTemplate() {
    setDownloading(true);
    setError('');
    try {
      await importService.descargarPlantilla();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo descargar la plantilla.');
    } finally {
      setDownloading(false);
    }
  }

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file || error) return;
    setBusy(true);
    setResult(null);
    try {
      setResult(await importService.validarArchivo(file));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo validar el archivo.');
    } finally {
      setBusy(false);
    }
  }

  return <main className="catalog-page import-page">
    <header className="catalog-header">
      <div>
        <p className="catalog-eyebrow">HOMOLOGACIONES FISEI · EXPEDIENTES ACADÉMICOS</p>
        <h1>Carga de historial académico</h1>
        <p>Descarga la plantilla y revisa los errores de cada fila antes de continuar.</p>
      </div>
      <Link to="/estudiantes" className="catalog-back-link">Volver a estudiantes</Link>
    </header>

    <section className="students-panel import-panel" aria-labelledby="template-heading">
      <div className="students-panel-heading">
        <div><h2 id="template-heading">1. Descarga la plantilla</h2><p>Completa las hojas Estudiante, Asignaturas y Contenidos. El archivo debe ser .xlsx y no superar 5 MB.</p></div>
        <button type="button" className="students-primary" onClick={downloadTemplate} disabled={downloading}>
          {downloading ? 'Descargando…' : 'Descargar plantilla'}
        </button>
      </div>
    </section>

    <section className="students-panel import-panel" aria-labelledby="upload-heading">
      <div className="students-panel-heading">
        <div><h2 id="upload-heading">2. Carga y valida el archivo</h2><p>El sistema te indicará la hoja, fila y columna que debes corregir.</p></div>
      </div>
      <form className="import-form" onSubmit={upload}>
        <label htmlFor="history-file">Archivo de historial (.xlsx)</label>
        <div className="import-controls">
          <input ref={fileInput} id="history-file" type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={chooseFile} />
          <button className="students-primary" type="submit" disabled={!file || Boolean(error) || busy}>
            {busy ? 'Validando…' : 'Cargar archivo'}
          </button>
          {file && <button className="students-secondary" type="button" onClick={() => { setFile(null); setResult(null); setError(''); if (fileInput.current) fileInput.current.value = ''; }} disabled={busy}>Quitar archivo</button>}
        </div>
        <span className="import-help">Se aceptan archivos .xlsx de hasta 5 MB. La validación no guarda cambios.</span>
      </form>

      {error && <p className="catalog-error" role="alert">{error}</p>}
      {result && <>
        <p className={result.errores.length ? 'import-summary import-summary-error' : 'import-summary'} role={result.errores.length ? 'alert' : 'status'}>
          {result.mensaje}
        </p>
        {result.errores.length > 0 && <div className="student-table-wrap import-errors-wrap">
          <table className="student-table import-errors-table">
            <thead><tr><th>Hoja</th><th>Fila</th><th>Columna</th><th>Error</th></tr></thead>
            <tbody>{result.errores.map((item, index) => <tr key={`${item.hoja}-${item.fila}-${item.columna}-${index}`}>
              <td>{item.hoja}</td><td>{item.fila || '—'}</td><td>{item.columna}</td><td>{item.mensaje}</td>
            </tr>)}</tbody>
          </table>
        </div>}
      </>}
    </section>
  </main>;
}
