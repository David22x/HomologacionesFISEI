import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { studentsService, type Estudiante } from '../studentsService';
import { catalogService, type Carrera, type MallaCurricular, type Asignatura } from '../../catalog/services/catalogService';
import { uploadImport } from '../services/fileImports';

interface Row { nombre: string; carrera: string; periodo: string; nota: number | null; estado: string; pagina: number; seleccionado: boolean; asignaturaId: string | null; creditos: number | null; horas: number | null; reemplazar: boolean; evidencia?: string; notaOriginal?: number | null; escalaOriginal?: number | null; creditosDocumento?: number | null }
interface Profile { nombres: string; apellidos: string; nombreCompleto: string; carrera: string; institucion: string; tipoIdentificacion: number; nivelActual: number | null; escalaMaxima: number | null }
interface Preview { identificacion: string; filas: Row[]; advertencias: string[]; perfil: Profile }
const emptyProfile = (): Profile => ({ nombres: '', apellidos: '', nombreCompleto: '', carrera: '', institucion: '', tipoIdentificacion: 1, nivelActual: null, escalaMaxima: null });
const key = (s: string) => s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/ingenieria|carrera|\b(de|en|la|las|el)\b/g, '').replace(/[^a-z0-9]/g, '');

export default function RecordImportPage() {
  const [students, setStudents] = useState<Estudiante[]>([]); const [studentId, setStudentId] = useState('');
  const [mode, setMode] = useState('registrar'); const [source, setSource] = useState('uta');
  const [careers, setCareers] = useState<Carrera[]>([]); const [careerId, setCareerId] = useState('');
  const [mallas, setMallas] = useState<MallaCurricular[]>([]); const [mallaId, setMallaId] = useState('');
  const [subjects, setSubjects] = useState<Asignatura[]>([]); const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null); const [busy, setBusy] = useState(false);
  const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [completed, setCompleted] = useState(false);
  const [showAll, setShowAll] = useState(false);
  const student = students.find(s => s.id === studentId);
  const external = mode === 'registrar' ? source === 'externo' : student?.tipo === 1;
  useEffect(() => { Promise.all([studentsService.listar(), catalogService.obtenerCarreras()]).then(([s, c]) => { setStudents(s); setCareers(c); }).catch(e => setError(e.message)); }, []);
  function edit(index: number, changes: Partial<Row>) { setPreview(p => p && ({ ...p, filas: p.filas.map((r, i) => i === index ? { ...r, ...changes } : r) })); }
  function profile(changes: Partial<Profile>) { setPreview(p => p && ({ ...p, perfil: { ...p.perfil, ...changes } })); }
  async function associate(malla: string, career: string, document: Preview) {
    const rows = await catalogService.obtenerAsignaturas(malla, '', ''); const carrera = careers.find(c => c.id === career);
    setSubjects(current => [...current.filter(a => !rows.some(b => a.id === b.id)), ...rows]);
    const mapped = document.filas.map(original => {
      const row = mode === 'registrar' ? { ...original, asignaturaId: null, horas: null } : original;
      if (row.carrera && carrera && key(row.carrera) !== key(carrera.nombre)) return row;
      const matches = rows.filter(a => key(a.nombre) === key(row.nombre));
      return matches.length === 1 ? { ...row, asignaturaId: matches[0].id, creditos: row.creditosDocumento ?? matches[0].creditos, horas: matches[0].horas } : row;
    });
    const levels = mapped.filter(r => r.seleccionado).map(r => rows.find(a => a.id === r.asignaturaId)?.nivelNumero ?? 0);
    const differences = mapped.filter(r => r.creditosDocumento != null && rows.some(a => a.id === r.asignaturaId && a.creditos !== r.creditosDocumento))
      .map(r => `Créditos distintos del catálogo en ${r.nombre}: PDF ${r.creditosDocumento}. Verifica la versión de malla; se conservará el valor revisado del documento.`);
    return { ...document, advertencias: [...new Set([...document.advertencias, ...differences])], filas: mapped, perfil: { ...document.perfil, nivelActual: document.perfil.nivelActual ?? (Math.max(0, ...levels) || null) } };
  }
  async function chooseCareer(id: string) {
    setCareerId(id); setMallaId(''); setMallas([]); setSubjects([]);
    if (mode === 'registrar') setPreview(p => p && ({ ...p, filas: p.filas.map(r => ({ ...r, asignaturaId: null, horas: null })) }));
    if (!id) return;
    setBusy(true); setError('');
    try { setMallas(await catalogService.obtenerMallas(id)); } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo cargar la malla.'); } finally { setBusy(false); }
  }
  async function chooseMalla(id: string) {
    setMallaId(id); if (!id || !preview) return; setBusy(true); setError('');
    try { setPreview(await associate(id, careerId, preview)); }
    catch (e) { setError(e instanceof Error ? e.message : 'No se pudo consultar el catálogo.'); } finally { setBusy(false); }
  }
  async function analyze() {
    if (!file) return; setBusy(true); setError(''); setNotice(''); setCompleted(false); setPreview(null); setShowAll(source === 'externo'); setCareerId(''); setMallaId(''); setMallas([]); setSubjects([]);
    try {
      const response = await uploadImport<Preview>(`/api/v1/documentos/records/analizar-registro?externo=${source === 'externo'}`, file);
      let document = { ...response, perfil: { ...emptyProfile(), ...response.perfil } };
      setPreview(document);
      if (source === 'uta' && mode === 'registrar') {
        const matches = careers.filter(c => key(c.nombre) === key(document.perfil.carrera));
        if (matches.length === 1) {
          const career = matches[0].id; setCareerId(career);
          const versions = await catalogService.obtenerMallas(career); setMallas(versions);
          const published = versions.filter(m => m.vigente); const candidates = published.length === 1 ? published : versions;
          if (candidates.length === 1) { setMallaId(candidates[0].id); document = await associate(candidates[0].id, career, document); setPreview(document); }
        }
      }
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo leer el récord.'); } finally { setBusy(false); }
  }
  async function confirm() {
    if (!preview || !file || (mode === 'actualizar' && !student)) return;
    setBusy(true); setError(''); setNotice('');
    try {
      if (mode === 'registrar') {
        await uploadImport('/api/v1/documentos/records/registrar', file, { documento: preview, tipo: external ? 1 : 0,
          carreraOrigenId: careerId || null, mallaOrigenId: mallaId || null, nivelActual: preview.perfil.nivelActual });
        setNotice('Estudiante y calificaciones registrados. Si la identificación ya existía, se conservó su historial y se incorporaron las materias seleccionadas.');
      } else {
        await uploadImport(`/api/v1/documentos/records/confirmar?externo=${source === 'externo'}`, file, { estudianteId: studentId, ...preview });
        setNotice('Récord importado. Se conservaron las materias existentes.');
      }
      setCompleted(true); setStudents(await studentsService.listar());
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo confirmar el récord.'); } finally { setBusy(false); }
  }
  function scale(value: string) {
    const max = Number(value) || null;
    setPreview(p => p && ({ ...p, perfil: { ...p.perfil, escalaMaxima: max }, filas: p.filas.map(r => r.notaOriginal != null
      ? { ...r, escalaOriginal: max, nota: max && r.notaOriginal >= 0 && r.notaOriginal <= max ? Math.round(r.notaOriginal * 1000 / max) / 100 : null } : r) }));
  }
  return <main className="catalog-page import-page"><header className="catalog-header"><div><h1>Registro y calificaciones desde PDF</h1><p>Sube el récord para precargar al estudiante y sus materias. Revisa los datos extraídos y completa únicamente lo que falte.</p></div></header>
    <section className="students-panel"><fieldset disabled={busy} className="student-form-grid">
      <label>Operación<select value={mode} onChange={e => { setMode(e.target.value); setPreview(null); setCompleted(false); }}><option value="registrar">Registrar estudiante desde PDF</option><option value="actualizar">Actualizar calificaciones de estudiante registrado</option></select></label>
      <label>Origen del documento<select value={source} onChange={e => { setSource(e.target.value); setPreview(null); setCompleted(false); }}><option value="uta">Universidad Técnica de Ambato</option><option value="externo">Otra universidad / extranjero (extracción con IA)</option></select></label>
      {mode === 'actualizar' && <label>Estudiante del récord<select value={studentId} onChange={e => { setStudentId(e.target.value); setPreview(null); }}><option value="">Selecciona un estudiante registrado</option>{students.map(s => <option key={s.id} value={s.id}>{s.apellidos}, {s.nombres} - {s.cedula}</option>)}</select></label>}
      <label>Archivo PDF<input type="file" accept=".pdf" onChange={e => { setFile(e.target.files?.[0] ?? null); setPreview(null); setError(''); setNotice(''); setCompleted(false); }} /></label>
      <button type="button" className="students-primary" disabled={!file || (mode === 'actualizar' && !studentId)} onClick={() => { void analyze(); }}>{busy ? 'Procesando documento…' : 'Leer récord académico'}</button>
    </fieldset><p>{source === 'externo' ? 'La IA extrae información del texto del PDF. Confirma las notas y la escala; los datos desconocidos quedan pendientes. Los documentos escaneados requieren OCR previo.' : 'Lectura de certificaciones UTA sin depender de IA. Las materias sin cursar se muestran aparte y no se seleccionan para importar.'}</p></section>
    {error && <p role="alert" className="catalog-error">{error}</p>}{notice && <p role="status" className="maintenance-notice">{notice}</p>}
    {preview && !completed && <section className="students-panel"><h2>Revisa la información extraída</h2>
      <fieldset disabled={busy} className="student-form-grid"><label>Identificación del documento<input value={preview.identificacion} onChange={e => setPreview({ ...preview, identificacion: e.target.value })} /></label>
      {mode === 'registrar' && <><label>Tipo de identificación<select value={preview.perfil.tipoIdentificacion} onChange={e => profile({ tipoIdentificacion: Number(e.target.value) })}><option value={0}>Cédula ecuatoriana</option><option value={1}>Pasaporte / identificación extranjera</option></select></label>
        {preview.perfil.nombreCompleto && <p>Nombre según documento: <strong>{preview.perfil.nombreCompleto}</strong></p>}
        <label>Nombres del estudiante<input value={preview.perfil.nombres} maxLength={150} onChange={e => profile({ nombres: e.target.value })} /></label><label>Apellidos del estudiante<input value={preview.perfil.apellidos} maxLength={150} onChange={e => profile({ apellidos: e.target.value })} /></label>
        <label>Universidad de origen<input value={preview.perfil.institucion} maxLength={200} onChange={e => profile({ institucion: e.target.value })} /></label><label>Carrera según documento<input value={preview.perfil.carrera} maxLength={200} onChange={e => profile({ carrera: e.target.value })} /></label>
        <label>Nivel actual (verificar)<input type="number" min={1} value={preview.perfil.nivelActual ?? ''} onChange={e => profile({ nivelActual: Number(e.target.value) || null })} /></label><p>Verifica la separación de nombres y apellidos. El nivel sugerido corresponde al mayor nivel de las materias asociadas; el documento puede no indicar el nivel actual.</p></>}
      </fieldset>
      {mode === 'actualizar' && preview.identificacion !== student?.cedula && <p role="alert">La identificación no coincide con el estudiante seleccionado. Verifícala antes de confirmar.</p>}
      <details><summary>Advertencias del documento ({preview.advertencias.length})</summary><ul>{preview.advertencias.map((a, i) => <li key={i}>{a}</li>)}</ul></details>
      {!external ? <fieldset disabled={busy} className="student-form-grid"><legend>Asociación con la malla de origen</legend>
        <label>Carrera del documento<select value={careerId} onChange={e => { void chooseCareer(e.target.value); }}><option value="">Selecciona si no fue reconocida</option>{careers.map(c => <option key={c.id} value={c.id}>{c.nombre}</option>)}</select></label>
        <label>Malla para asociar<select value={mallaId} onChange={e => { void chooseMalla(e.target.value); }}><option value="">Confirma la malla correspondiente</option>{mallas.map(m => <option key={m.id} value={m.id}>{m.version}</option>)}</select></label>
      </fieldset> : <><label>Escala máxima del reporte (solo si está verificada)<input type="number" min={1} value={preview.perfil.escalaMaxima ?? ''} disabled={busy} onChange={e => scale(e.target.value)} /></label><p>La conversión sugerida es proporcional a 0–10; verifica que corresponda a la equivalencia institucional. Si falta la escala, confírmala o ingresa manualmente la nota equivalente. Créditos, horas y contenidos desconocidos pueden guardarse pendientes; el análisis de homologación quedará bloqueado hasta completarlos.</p></>}
      <div className="students-panel-heading"><p>{preview.filas.filter(r => r.seleccionado).length} materias seleccionadas de {preview.filas.length}</p><label><input type="checkbox" checked={showAll} onChange={e => setShowAll(e.target.checked)} />Mostrar también materias sin cursar, reprobadas o pendientes</label></div>
      <div className="student-table-wrap"><table className="student-table"><thead><tr><th>Importar</th><th>Asignatura / catálogo</th><th>Carrera / período</th><th>Nota sobre 10 / estado</th><th>Créditos / horas</th><th>Detalle</th></tr></thead><tbody>{preview.filas.map((r, i) => (showAll || r.estado === 'APROBADO') && <tr key={i}>
        <td><input type="checkbox" aria-label={`Importar ${r.nombre}`} checked={r.seleccionado} disabled={busy} onChange={e => edit(i, { seleccionado: e.target.checked })} /><small>Página {r.pagina}</small></td>
        <td><input aria-label={`Nombre fila ${i + 1}`} value={r.nombre} disabled={busy} onChange={e => edit(i, { nombre: e.target.value })} />{!external && <select aria-label={`Catálogo fila ${i + 1}`} disabled={busy} value={r.asignaturaId ?? ''} onChange={e => { const a = subjects.find(s => s.id === e.target.value); edit(i, { asignaturaId: a?.id ?? null, creditos: r.creditosDocumento ?? a?.creditos ?? null, horas: a?.horas ?? null }); }}><option value="">Pendiente: selecciona una asignatura</option>{subjects.map(a => <option key={a.id} value={a.id}>{a.codigo} - {a.nombre}</option>)}</select>}</td>
        <td><input aria-label={`Carrera fila ${i + 1}`} value={r.carrera} disabled={busy} onChange={e => edit(i, { carrera: e.target.value })} /><input aria-label={`Período fila ${i + 1}`} value={r.periodo} disabled={busy} onChange={e => edit(i, { periodo: e.target.value })} /></td>
        <td>{r.notaOriginal != null && <small>Original: {r.notaOriginal} / {r.escalaOriginal ?? 'escala pendiente'}</small>}<input aria-label={`Nota fila ${i + 1}`} type="number" min={0} max={10} step="0.01" value={r.nota ?? ''} disabled={busy} onChange={e => edit(i, { nota: e.target.value ? Number(e.target.value) : null })} /><select aria-label={`Estado fila ${i + 1}`} value={r.estado} disabled={busy} onChange={e => edit(i, { estado: e.target.value })}><option>APROBADO</option><option>REPROBADO</option><option>PENDIENTE</option><option>SIN CURSAR</option></select></td>
        <td>{r.creditosDocumento != null && <small>Créditos según PDF: {r.creditosDocumento}</small>}<input aria-label={`Créditos fila ${i + 1}`} type="number" min="0.01" step="0.01" value={r.creditos ?? ''} disabled={busy} onChange={e => edit(i, { creditos: e.target.value ? Number(e.target.value) : null })} /><input aria-label={`Horas fila ${i + 1}`} type="number" min={1} value={r.horas ?? ''} disabled={busy} onChange={e => edit(i, { horas: e.target.value ? Number(e.target.value) : null })} /></td>
        <td><details><summary>Ver detalle</summary>{r.evidencia && <blockquote>{r.evidencia}</blockquote>}<label><input type="checkbox" checked={r.reemplazar} disabled={busy} onChange={e => edit(i, { reemplazar: e.target.checked })} />Autorizar cambio de nota o período existente</label></details></td></tr>)}</tbody></table></div>
      <button type="button" disabled={busy} onClick={() => { setShowAll(true); setPreview({ ...preview, filas: [...preview.filas, { nombre: '', carrera: preview.perfil.carrera, periodo: '', nota: null, estado: 'PENDIENTE', pagina: 1, seleccionado: false, asignaturaId: null, creditos: null, horas: null, reemplazar: false }] }); }}>Agregar asignatura faltante</button>
      <button type="button" className="students-primary" disabled={busy || (mode === 'actualizar' && preview.identificacion !== student?.cedula) || !preview.filas.some(r => r.seleccionado)} onClick={() => { void confirm(); }}>{mode === 'registrar' ? 'Confirmar registro y calificaciones' : 'Confirmar materias seleccionadas'}</button>
    </section>}
    {completed && <p><Link to="/estudiantes">Ver estudiantes registrados y completar datos pendientes</Link></p>}
  </main>;
}
