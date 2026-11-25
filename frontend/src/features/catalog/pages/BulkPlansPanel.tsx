import { useEffect, useState } from 'react';
import { catalogService, type Carrera, type MallaCurricular, type Asignatura, type PlanAnaliticoPreview } from '../services/catalogService';
import { uploadImport } from '../../students/services/fileImports';

type Plan = PlanAnaliticoPreview & { nivel: number | null; carrera?: string };
interface Entry { archivo: string; asignaturaId: string | null; datos: Plan | null; error: string | null; seleccionado: boolean; reemplazar: boolean; tienePlan: boolean }

export default function BulkPlansPanel() {
  const [careers, setCareers] = useState<Carrera[]>([]); const [career, setCareer] = useState('');
  const [mallas, setMallas] = useState<MallaCurricular[]>([]); const [malla, setMalla] = useState('');
  const [subjects, setSubjects] = useState<Asignatura[]>([]); const [file, setFile] = useState<File | null>(null);
  const [entries, setEntries] = useState<Entry[]>([]); const [busy, setBusy] = useState(false);
  const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  useEffect(() => { catalogService.obtenerCarreras().then(setCareers).catch(e => setError(e.message)); }, []);
  useEffect(() => { let active = true;
    if (career) catalogService.obtenerMallas(career).then(rows => { if (active) setMallas(rows.filter(m => m.estado !== 'Publicada')); }).catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [career]);
  useEffect(() => { let active = true;
    if (malla) catalogService.obtenerAsignaturas(malla, '', '').then(rows => { if (active) setSubjects(rows); }).catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [malla]);
  function update(index: number, changes: Partial<Entry>) { setEntries(current => current.map((row, i) => i === index ? { ...row, ...changes } : row)); }
  function edit(index: number, changes: Partial<Plan>) { setEntries(current => current.map((row, i) => i === index && row.datos ? { ...row, datos: { ...row.datos, ...changes } } : row)); }
  async function run(confirm: boolean) {
    if (!file || !malla) return;
    setBusy(true); setError(''); setNotice('');
    try {
      if (!confirm) {
        const result = await uploadImport<{ entradas: Entry[]; mensaje: string }>(`/api/v1/catalogo/mallas/${malla}/planes-analiticos/analizar`, file);
        setEntries(result.entradas); setNotice(result.mensaje);
      } else {
        const result = await uploadImport<{ resultados: { archivo: string; estado: string }[] }>(`/api/v1/catalogo/mallas/${malla}/planes-analiticos/confirmar`, file, entries.map(row => ({ ...row, datos: row.datos && { ...row.datos, temas: row.datos.temas.map(t => ({ ...t, subtemas: t.subtemas.map(s => s.trim()).filter(Boolean) })) } }))); 
        setNotice(`Carga completada: ${result.resultados.filter(r => r.estado === 'Importado').length} planes importados, ${result.resultados.filter(r => r.estado === 'Sin cambios').length} ya estaban cargados.`);
        setEntries([]); setSubjects(await catalogService.obtenerAsignaturas(malla, '', ''));
      }
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo procesar el ZIP.'); }
    finally { setBusy(false); }
  }
  return <section className="students-panel import-panel" aria-label="Carga masiva de planes analíticos">
    <h2>Importar planes analíticos desde ZIP</h2><p>Selecciona la carrera y una malla borrador. Se revisan todos los PDF antes de guardar. Máximo 30 MB, 200 PDF.</p>
    <fieldset disabled={busy} className="student-form-grid"><label>Carrera del ZIP<select value={career} onChange={e => { setCareer(e.target.value); setMallas([]); setMalla(''); setEntries([]); setSubjects([]); }}><option value="">Selecciona</option>{careers.map(c => <option key={c.id} value={c.id}>{c.nombre}</option>)}</select></label>
      <label>Malla del ZIP<select value={malla} onChange={e => { setMalla(e.target.value); setEntries([]); setSubjects([]); }}><option value="">Selecciona</option>{mallas.map(m => <option key={m.id} value={m.id}>{m.version} {m.estado ?? ''}</option>)}</select></label>
      <label>Archivo ZIP<input type="file" accept=".zip" onChange={e => { setFile(e.target.files?.[0] ?? null); setEntries([]); setError(''); }} /></label>
      <button type="button" className="students-primary" disabled={!file || !malla} onClick={() => { void run(false); }}>{busy ? 'Procesando…' : 'Analizar ZIP'}</button>
    </fieldset>
    {error && <p role="alert" className="catalog-error">{error}</p>}{notice && <p role="status" className="maintenance-notice">{notice}</p>}
    {entries.length > 0 && <><p>{entries.filter(e => e.datos).length} PDF interpretados de {entries.length}. Abre cada detalle para revisar los contenidos.</p>
      <div className="student-table-wrap"><table className="student-table"><thead><tr><th>Importar</th><th>Archivo y asignatura</th><th>Datos extraídos</th><th>Contenidos / reemplazo</th></tr></thead>
        <tbody>{entries.map((row, i) => <tr key={row.archivo}><td><input type="checkbox" aria-label={`Importar ${row.archivo}`} checked={row.seleccionado} disabled={busy || !row.datos} onChange={e => update(i, { seleccionado: e.target.checked })} /></td>
          <td>{row.archivo}{row.error && <p role="alert">{row.error}</p>}{row.datos && <label>Asociación<select value={row.asignaturaId ?? ''} disabled={busy} onChange={e => update(i, { asignaturaId: e.target.value || null })}><option value="">Crear asignatura y nivel si no existen</option>{subjects.map(a => <option key={a.id} value={a.id}>{a.codigo} — {a.nombre}</option>)}</select></label>}</td>
          <td>{row.datos && <fieldset disabled={busy}><label>Nombre<input value={row.datos.nombre} onChange={e => edit(i, { nombre: e.target.value })} /></label><label>Código<input maxLength={30} value={row.datos.codigo} onChange={e => edit(i, { codigo: e.target.value })} /></label>
            <label>Nivel<input type="number" min={1} max={20} value={row.datos.nivel ?? ''} onChange={e => edit(i, { nivel: e.target.value ? Number(e.target.value) : null })} /></label>
            <label>Créditos<input type="number" min="0.01" step="0.01" value={row.datos.creditos || ''} onChange={e => edit(i, { creditos: Number(e.target.value) })} /></label><label>Horas<input type="number" min={1} value={row.datos.horas || ''} onChange={e => edit(i, { horas: Number(e.target.value) })} /></label></fieldset>}</td>
          <td>{row.datos && <><details><summary>{row.datos.temas.length} temas: revisar y corregir</summary>{row.datos.temas.map((t, n) => <div key={n}><input aria-label={`Tema ${n + 1} de ${row.archivo}`} value={t.nombre} disabled={busy} onChange={e => edit(i, { temas: row.datos!.temas.map((x, j) => j === n ? { ...x, nombre: e.target.value } : x) })} /><textarea aria-label={`Subtemas ${n + 1} de ${row.archivo}`} disabled={busy} value={t.subtemas.join('\n')} onChange={e => edit(i, { temas: row.datos!.temas.map((x, j) => j === n ? { ...x, subtemas: e.target.value.split('\n') } : x) })} /></div>)}</details>
            {row.tienePlan && <label><input type="checkbox" checked={row.reemplazar} disabled={busy} onChange={e => update(i, { reemplazar: e.target.checked })} />Autorizar reemplazo del plan existente</label>}</>}</td></tr>)}</tbody></table></div>
      <button type="button" className="students-primary" disabled={busy || !entries.some(r => r.seleccionado && r.datos)} onClick={() => { void run(true); }}>Confirmar planes seleccionados</button></>}
  </section>;
}
