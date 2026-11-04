import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { importService, type ImportPreview, type ImportResult } from '../services/importService';

const MAX_FILE_SIZE = 5 * 1024 * 1024;

export default function ImportHistoryPage() {
  const fileInput = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState('');
  const [confirmationMessage, setConfirmationMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const preview = result?.errores.length === 0 ? result.vistaPrevia : null;

  function chooseFile(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0] ?? null;
    setFile(selected);
    setResult(null);
    setError('');
    setConfirmationMessage('');
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
    setConfirmationMessage('');
    try {
      setResult(await importService.validarArchivo(file));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo validar el archivo.');
    } finally {
      setBusy(false);
    }
  }

  async function confirmImport() {
    if (!file || !preview || busy || confirmationMessage) return;
    setBusy(true);
    setError('');
    try {
      const response = await importService.confirmarArchivo(file);
      setConfirmationMessage(response.creado
        ? 'Importación confirmada. El estudiante y su historial quedaron guardados.'
        : 'Importación confirmada. El estudiante ya existía y no se creó un registro duplicado.');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo confirmar la importación.');
    } finally {
      setBusy(false);
    }
  }

  function cancelImport() {
    setFile(null);
    setResult(null);
    setError('');
    setConfirmationMessage('');
    if (fileInput.current) fileInput.current.value = '';
  }

  return <main className="catalog-page import-page">
    <header className="catalog-header">
      <div>
        <p className="catalog-eyebrow">HOMOLOGACIONES FISEI · EXPEDIENTES ACADÉMICOS</p>
        <h1>Carga de historial académico</h1>
        <p>Descarga la plantilla, revisa los datos y confirma la importación.</p>
      </div>
      <Link to="/estudiantes" className="catalog-back-link">Volver a estudiantes</Link>
    </header>

    <section className="students-panel import-panel" aria-labelledby="template-heading">
      <div className="students-panel-heading">
        <div><h2 id="template-heading">1. Descarga la plantilla</h2><p>Completa las hojas Estudiante, Asignaturas y Contenidos. El archivo debe ser .xlsx y no superar 5 MB.</p></div>
        <button type="button" className="students-primary" onClick={downloadTemplate} disabled={downloading || busy}>
          {downloading ? 'Descargando…' : 'Descargar plantilla'}
        </button>
      </div>
    </section>

    <section className="students-panel import-panel" aria-labelledby="upload-heading">
      <div className="students-panel-heading">
        <div><h2 id="upload-heading">2. Carga y valida el archivo</h2><p>Si hay errores, corrige el Excel y vuelve a cargarlo. Un archivo válido mostrará una vista previa antes de guardarlo.</p></div>
      </div>
      <form className="import-form" onSubmit={upload}>
        <label htmlFor="history-file">Archivo de historial (.xlsx)</label>
        <div className="import-controls">
          <input ref={fileInput} id="history-file" type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={chooseFile} disabled={busy || Boolean(confirmationMessage)} />
          <button className="students-primary" type="submit" disabled={!file || Boolean(error) || busy || Boolean(confirmationMessage)}>
            {busy ? 'Validando…' : 'Cargar archivo'}
          </button>
          {file && !confirmationMessage && <button className="students-secondary" type="button" onClick={cancelImport} disabled={busy}>Quitar archivo</button>}
        </div>
        {file && <span className="import-help">Archivo seleccionado: {file.name}</span>}
        <span className="import-help">Se aceptan archivos .xlsx de hasta 5 MB. La carga inicial solo valida y prepara la vista previa.</span>
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
        {result.errores.length === 0 && preview && <PreviewPanel preview={preview} />}
      </>}

      {preview && !confirmationMessage && <div className="import-actions">
        <button type="button" className="students-primary" onClick={confirmImport} disabled={busy}>
          {busy ? 'Confirmando…' : 'Confirmar importación'}
        </button>
        <button type="button" className="students-secondary" onClick={cancelImport} disabled={busy}>Cancelar</button>
      </div>}
      {confirmationMessage && <p className="import-summary" role="status">{confirmationMessage}</p>}
    </section>
  </main>;
}

function PreviewPanel({ preview }: { preview: ImportPreview }) {
  return <section className="import-preview" aria-labelledby="preview-heading">
    <div className="import-preview-heading">
      <div><p className="catalog-eyebrow">VISTA PREVIA · SOLO LECTURA</p><h2 id="preview-heading">Revisa los datos normalizados</h2></div>
      <span>{preview.historial.asignaturas.length} asignatura(s)</span>
    </div>
    <dl className="import-preview-details">
      <div><dt>Estudiante</dt><dd>{preview.apellidos}, {preview.nombres}</dd></div>
      <div><dt>Identificación</dt><dd>{preview.cedula} · {preview.tipoIdentificacion === 0 ? 'Cédula' : 'Pasaporte'}</dd></div>
      <div><dt>Institución de origen</dt><dd>{preview.historial.institucionOrigen || '—'}</dd></div>
      <div><dt>Carrera de origen</dt><dd>{preview.historial.carreraOrigenNombre || '—'}</dd></div>
      <div><dt>Nivel actual</dt><dd>{preview.historial.nivelActual ?? '—'}</dd></div>
    </dl>
    <h3>Asignaturas aprobadas</h3>
    <div className="student-table-wrap">
      <table className="student-table">
        <thead><tr><th>Código</th><th>Asignatura</th><th>Créditos</th><th>Horas</th><th>Nota</th><th>Período</th></tr></thead>
        <tbody>{preview.historial.asignaturas.map((subject, index) => <tr key={`${subject.codigo}-${index}`}>
          <td>{subject.codigo || '—'}</td><td>{subject.nombre}</td><td>{subject.creditos ?? '—'}</td>
          <td>{subject.horas ?? '—'}</td><td>{subject.nota}</td><td>{subject.periodo}</td>
        </tr>)}</tbody>
      </table>
    </div>
  </section>;
}
