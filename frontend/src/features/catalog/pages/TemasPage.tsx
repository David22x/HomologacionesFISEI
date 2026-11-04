import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import {
  catalogService,
  type Asignatura,
  type Subtema,
  type Tema,
  type TemaWrite,
} from '../services/catalogService';

interface TemasPageProps {
  isAdmin: boolean;
}

interface TopicFormState {
  nombre: string;
  orden: string;
  horasEstimadas: string;
}

const emptyForm: TopicFormState = { nombre: '', orden: '', horasEstimadas: '' };

export default function TemasPage({ isAdmin }: TemasPageProps) {
  const { asignaturaId = '' } = useParams();
  const location = useLocation();
  const assignmentFromNavigation = (location.state as { asignatura?: Asignatura } | null)?.asignatura;
  const [temas, setTemas] = useState<Tema[]>([]);
  const [subtemas, setSubtemas] = useState<Record<string, Subtema[]>>({});
  const [temaForm, setTemaForm] = useState<TopicFormState>(emptyForm);
  const [subtemaForm, setSubtemaForm] = useState<TopicFormState>(emptyForm);
  const [editingTemaId, setEditingTemaId] = useState('');
  const [editingSubtemaId, setEditingSubtemaId] = useState('');
  const [subtemaParentId, setSubtemaParentId] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const cargarDatos = useCallback(async () => {
    if (!asignaturaId) {
      setError('No se indicó la asignatura que se desea consultar.');
      setLoading(false);
      return;
    }

    setLoading(true);
    setError('');
    try {
      const temasActuales = await catalogService.obtenerTemas(asignaturaId);
      setTemas(temasActuales);
      const pares = await Promise.all(
        temasActuales.map(async (tema) => [tema.id, await catalogService.obtenerSubtemas(tema.id)] as const),
      );
      setSubtemas(Object.fromEntries(pares));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron cargar los temas y subtemas.');
    } finally {
      setLoading(false);
    }
  }, [asignaturaId]);

  useEffect(() => { void cargarDatos(); }, [cargarDatos]);

  const temaOrdenado = useMemo(() => [...temas].sort((a, b) => a.orden - b.orden || a.nombre.localeCompare(b.nombre)), [temas]);

  function writePayload(form: TopicFormState): TemaWrite {
    return {
      nombre: form.nombre.trim(),
      orden: Number(form.orden),
      horasEstimadas: form.horasEstimadas.trim() ? Number(form.horasEstimadas) : null,
    };
  }

  function editarTema(tema: Tema) {
    setSubtemaParentId(''); setEditingSubtemaId(''); setSubtemaForm(emptyForm);
    setEditingTemaId(tema.id);
    setTemaForm({ nombre: tema.nombre, orden: String(tema.orden), horasEstimadas: tema.horasEstimadas == null ? '' : String(tema.horasEstimadas) });
    setNotice('');
    setError('');
  }

  function iniciarNuevoTema() {
    setEditingTemaId(''); setSubtemaParentId(''); setEditingSubtemaId(''); setSubtemaForm(emptyForm);
    setEditingTemaId('');
    setTemaForm({ ...emptyForm, orden: String((temas.reduce((max, tema) => Math.max(max, tema.orden), 0) || 0) + 1) });
    setNotice('');
    setError('');
  }

  async function guardarTema(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true); setError(''); setNotice('');
    try {
      if (editingTemaId) await catalogService.actualizarTema(editingTemaId, writePayload(temaForm));
      else await catalogService.crearTema(asignaturaId, writePayload(temaForm));
      setTemaForm(emptyForm); setEditingTemaId('');
      setNotice(editingTemaId ? 'Tema actualizado.' : 'Tema agregado.');
      await cargarDatos();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar el tema.');
    } finally { setBusy(false); }
  }

  function iniciarSubtema(tema: Tema) {
    setEditingTemaId(''); setTemaForm(emptyForm);
    setSubtemaParentId(tema.id);
    setEditingSubtemaId('');
    setSubtemaForm({ ...emptyForm, orden: String(((subtemas[tema.id] ?? []).reduce((max, item) => Math.max(max, item.orden), 0) || 0) + 1) });
    setNotice(''); setError('');
  }

  function editarSubtema(tema: Tema, subtema: Subtema) {
    setEditingTemaId(''); setTemaForm(emptyForm);
    setSubtemaParentId(tema.id);
    setEditingSubtemaId(subtema.id);
    setSubtemaForm({ nombre: subtema.nombre, orden: String(subtema.orden), horasEstimadas: subtema.horasEstimadas == null ? '' : String(subtema.horasEstimadas) });
    setNotice(''); setError('');
  }

  async function guardarSubtema(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true); setError(''); setNotice('');
    try {
      if (editingSubtemaId) await catalogService.actualizarSubtema(editingSubtemaId, writePayload(subtemaForm));
      else await catalogService.crearSubtema(subtemaParentId, writePayload(subtemaForm));
      setSubtemaForm(emptyForm); setEditingSubtemaId(''); setSubtemaParentId('');
      setNotice(editingSubtemaId ? 'Subtema actualizado.' : 'Subtema agregado.');
      await cargarDatos();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar el subtema.');
    } finally { setBusy(false); }
  }

  async function moverTema(index: number, delta: number) {
    const destino = index + delta;
    if (destino < 0 || destino >= temaOrdenado.length) return;
    const reordered = [...temaOrdenado];
    [reordered[index], reordered[destino]] = [reordered[destino], reordered[index]];
    setBusy(true); setError(''); setNotice('');
    try {
      for (const [position, tema] of reordered.entries()) {
        await catalogService.actualizarTema(tema.id, { nombre: tema.nombre, orden: position + 1, horasEstimadas: tema.horasEstimadas });
      }
      setNotice('Orden de temas actualizado.');
      await cargarDatos();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cambiar el orden de los temas.');
      await cargarDatos();
    } finally { setBusy(false); }
  }

  async function moverSubtema(temaId: string, index: number, delta: number) {
    const ordered = [...(subtemas[temaId] ?? [])].sort((a, b) => a.orden - b.orden || a.nombre.localeCompare(b.nombre));
    const destino = index + delta;
    if (destino < 0 || destino >= ordered.length) return;
    [ordered[index], ordered[destino]] = [ordered[destino], ordered[index]];
    setBusy(true); setError(''); setNotice('');
    try {
      for (const [position, subtema] of ordered.entries()) {
        await catalogService.actualizarSubtema(subtema.id, { nombre: subtema.nombre, orden: position + 1, horasEstimadas: subtema.horasEstimadas });
      }
      setNotice('Orden de subtemas actualizado.');
      await cargarDatos();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cambiar el orden de los subtemas.');
      await cargarDatos();
    } finally { setBusy(false); }
  }

  async function eliminarTema(tema: Tema) {
    if (!window.confirm(`¿Eliminar el tema «${tema.nombre}» y todos sus subtemas?`)) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await catalogService.eliminarTema(tema.id);
      setNotice('Tema y subtemas eliminados.');
      await cargarDatos();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar el tema.');
    } finally { setBusy(false); }
  }

  async function eliminarSubtema(subtema: Subtema) {
    if (!window.confirm(`¿Eliminar el subtema «${subtema.nombre}»?`)) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await catalogService.eliminarSubtema(subtema.id);
      setNotice('Subtema eliminado.');
      await cargarDatos();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar el subtema.');
    } finally { setBusy(false); }
  }

  function renderFormulario(form: TopicFormState, onChange: (value: TopicFormState) => void, title: string, onSubmit: (event: FormEvent<HTMLFormElement>) => void, onCancel: () => void) {
    return <form className="topics-form" onSubmit={onSubmit}>
      <h3>{title}</h3>
      <label>Nombre<input value={form.nombre} onChange={event => onChange({ ...form, nombre: event.target.value })} maxLength={300} required /></label>
      <div className="topics-form-row">
        <label>Orden<input type="number" value={form.orden} onChange={event => onChange({ ...form, orden: event.target.value })} min={1} step={1} required /></label>
        <label>Horas estimadas (opcional)<input type="number" value={form.horasEstimadas} onChange={event => onChange({ ...form, horasEstimadas: event.target.value })} min={1} step={1} placeholder="Sin especificar" /></label>
      </div>
      <div className="maintenance-actions"><button className="maintenance-primary" disabled={busy}>{busy ? 'Guardando…' : 'Guardar'}</button><button type="button" onClick={onCancel} disabled={busy}>Cancelar</button></div>
    </form>;
  }

  return <main className="catalog-page topics-page">
    <header className="catalog-header">
      <div><p className="catalog-eyebrow">HOMOLOGACIONES FISEI · CATÁLOGO ACADÉMICO</p><h1>Unidades temáticas</h1>
        <p>{assignmentFromNavigation ? <><strong>{assignmentFromNavigation.codigo}</strong> · {assignmentFromNavigation.nombre} · Nivel {assignmentFromNavigation.nivelNumero}</> : `Asignatura ${asignaturaId}`}</p>
      </div>
      <Link to="/catalogo" className="catalog-back-link">Volver al catálogo</Link>
    </header>

    <section className="topics-content">
      <div className="topics-heading"><div><h2>Temas y subtemas</h2><p>{isAdmin ? 'Puedes agregar, editar, reordenar y eliminar el contenido.' : 'Vista de consulta de temas y subtemas.'}</p></div>
        {isAdmin && <button type="button" className="maintenance-primary" onClick={iniciarNuevoTema}>Agregar tema</button>}
      </div>
      {error && <div role="alert" className="catalog-error">{error}</div>}
      {notice && <div role="status" className="maintenance-notice">{notice}</div>}
      {isAdmin && editingTemaId === '' && temaForm.orden && renderFormulario(temaForm, setTemaForm, 'Nuevo tema', guardarTema, () => setTemaForm(emptyForm))}
      {loading ? <p className="catalog-message" role="status">Cargando temas y subtemas…</p> : temaOrdenado.length === 0 ? <p className="catalog-message">Esta asignatura aún no tiene temas registrados.</p> : (
        <div className="topics-list">
          {temaOrdenado.map((tema, index) => {
            const children = [...(subtemas[tema.id] ?? [])].sort((a, b) => a.orden - b.orden || a.nombre.localeCompare(b.nombre));
            return <article className="topic-card" key={tema.id}>
              <div className="topic-card-header">
                <div className="topic-title"><span className="topic-order">{tema.orden}</span><div><h3>{tema.nombre}</h3><p>{tema.horasEstimadas == null ? 'Horas no especificadas' : `${tema.horasEstimadas} horas estimadas`}</p></div></div>
                {isAdmin && <div className="topic-actions">
                  <button type="button" title="Mover tema arriba" aria-label={`Mover ${tema.nombre} arriba`} disabled={busy || index === 0} onClick={() => void moverTema(index, -1)}>↑</button>
                  <button type="button" title="Mover tema abajo" aria-label={`Mover ${tema.nombre} abajo`} disabled={busy || index === temaOrdenado.length - 1} onClick={() => void moverTema(index, 1)}>↓</button>
                  <button type="button" onClick={() => editarTema(tema)} disabled={busy}>Editar</button>
                  <button type="button" onClick={() => iniciarSubtema(tema)} disabled={busy}>Agregar subtema</button>
                  <button type="button" className="topic-delete" onClick={() => void eliminarTema(tema)} disabled={busy}>Eliminar</button>
                </div>}
              </div>
              {editingTemaId === tema.id && isAdmin && renderFormulario(temaForm, setTemaForm, 'Editar tema', guardarTema, () => { setEditingTemaId(''); setTemaForm(emptyForm); })}
              <section className="subtopics-section" aria-label={`Subtemas de ${tema.nombre}`}>
                <h4>Subtemas <span>{children.length}</span></h4>
                {children.length === 0 ? <p className="subtopics-empty">No hay subtemas registrados.</p> : <ol className="subtopics-list">
                  {children.map((subtema, subIndex) => <li key={subtema.id}>
                    <div><span className="subtopic-order">{subtema.orden}.</span><span>{subtema.nombre}</span>{subtema.horasEstimadas != null && <small>{subtema.horasEstimadas} h</small>}</div>
                    {isAdmin && <div className="topic-actions compact">
                      <button type="button" aria-label={`Mover ${subtema.nombre} arriba`} disabled={busy || subIndex === 0} onClick={() => void moverSubtema(tema.id, subIndex, -1)}>↑</button>
                      <button type="button" aria-label={`Mover ${subtema.nombre} abajo`} disabled={busy || subIndex === children.length - 1} onClick={() => void moverSubtema(tema.id, subIndex, 1)}>↓</button>
                      <button type="button" onClick={() => editarSubtema(tema, subtema)} disabled={busy}>Editar</button>
                      <button type="button" className="topic-delete" onClick={() => void eliminarSubtema(subtema)} disabled={busy}>Eliminar</button>
                    </div>}
                    {editingSubtemaId === subtema.id && isAdmin && renderFormulario(subtemaForm, setSubtemaForm, 'Editar subtema', guardarSubtema, () => { setEditingSubtemaId(''); setSubtemaParentId(''); setSubtemaForm(emptyForm); })}
                  </li>)}
                </ol>}
                {subtemaParentId === tema.id && isAdmin && !editingSubtemaId && renderFormulario(subtemaForm, setSubtemaForm, 'Nuevo subtema', guardarSubtema, () => { setSubtemaParentId(''); setSubtemaForm(emptyForm); })}
              </section>
            </article>;
          })}
        </div>
      )}
    </section>
  </main>;
}
