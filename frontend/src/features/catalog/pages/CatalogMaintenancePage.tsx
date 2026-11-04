import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { catalogService, type Asignatura, type Carrera, type MallaCurricular, type Nivel } from '../services/catalogService';
import SearchableSelect from '../../../components/SearchableSelect';

type Section = 'carreras' | 'mallas' | 'niveles' | 'asignaturas';

export default function CatalogMaintenancePage() {
  const [section, setSection] = useState<Section>('carreras');
  const [carreras, setCarreras] = useState<Carrera[]>([]);
  const [mallas, setMallas] = useState<MallaCurricular[]>([]);
  const [niveles, setNiveles] = useState<Nivel[]>([]);
  const [asignaturas, setAsignaturas] = useState<Asignatura[]>([]);
  const [carreraId, setCarreraId] = useState('');
  const [mallaId, setMallaId] = useState('');
  const [nivelId, setNivelId] = useState('');
  const [editId, setEditId] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [searchText, setSearchText] = useState('');

  async function cargarCarreras() {
    const rows = await catalogService.obtenerCarreras();
    setCarreras(rows);
  }

  async function cargarMallas(id = carreraId) {
    if (!id) return setMallas([]);
    const rows = await catalogService.obtenerMallas(id);
    setMallas(rows);
  }

  async function cargarNiveles(id = mallaId) {
    if (!id) return setNiveles([]);
    const rows = await catalogService.obtenerNiveles(id);
    setNiveles(rows);
  }

  async function cargarAsignaturas(id = mallaId) {
    if (!id) return setAsignaturas([]);
    setAsignaturas(await catalogService.obtenerAsignaturas(id, nivelId, ''));
  }

  async function cargarTodo() {
    try {
      await cargarCarreras();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar el catálogo.');
    }
  }

  useEffect(() => { void cargarTodo(); }, []);
  useEffect(() => { void cargarMallas(); }, [carreraId]);
  useEffect(() => { void cargarNiveles(); }, [mallaId]);
  useEffect(() => { void cargarAsignaturas(); }, [mallaId, nivelId]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    setBusy(true); setError(''); setNotice('');
    try {
      if (section === 'carreras') {
        await catalogService.guardarCarrera(editId || undefined, {
          codigo: String(data.get('codigo')).trim(), nombre: String(data.get('nombre')).trim(), esDemo: data.get('esDemo') === 'on',
        });
      } else if (section === 'mallas') {
        await catalogService.guardarMalla(editId || undefined, carreraId, String(data.get('version')).trim());
      } else if (section === 'niveles') {
        await catalogService.guardarNivel(editId || undefined, mallaId, Number(data.get('numero')));
      } else {
        await catalogService.guardarAsignatura(editId || undefined, mallaId, {
          nivelId, codigo: String(data.get('codigo')).trim(), nombre: String(data.get('nombre')).trim(),
          creditos: Number(data.get('creditos')), horas: Number(data.get('horas')),
          unidadOrganizacional: String(data.get('unidad') || '').trim() || null,
        });
      }
      setEditId(''); setNotice(editId ? 'Cambios guardados.' : 'Registro creado.');
      form.reset();
      await cargarCarreras(); await cargarMallas(); await cargarNiveles(); await cargarAsignaturas();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar el registro.');
    } finally { setBusy(false); }
  }

  function editar(item: Carrera | MallaCurricular | Nivel | Asignatura) {
    setEditId(item.id);
    // Carrera and Asignatura both have a `codigo`; use the active section to
    // distinguish them so editing an assignment never switches to Carreras.
    if (section === 'asignaturas') {
      const asignatura = item as Asignatura;
      setMallaId(asignatura.mallaVersionId);
      setNivelId(asignatura.nivelId);
    } else if (section === 'carreras') {
      setSection('carreras');
    } else if (section === 'mallas') {
      setSection('mallas');
    } else {
      setSection('niveles');
    }
    setNotice(''); setError('');
    window.setTimeout(() => {
      const form = document.querySelector<HTMLFormElement>('.maintenance-form');
      if (!form) return;
      Object.entries(item).forEach(([key, value]) => {
        const field = form.elements.namedItem(({ nombre: 'nombre', codigo: 'codigo', version: 'version', numero: 'numero', creditos: 'creditos', horas: 'horas', unidadOrganizacional: 'unidad' } as Record<string, string>)[key] || key) as HTMLInputElement | null;
        if (field && field.type === 'checkbox') field.checked = Boolean(value);
        else if (field && value != null) field.value = String(value);
      });
    }, 0);
  }

  async function publicar(malla: MallaCurricular) {
    setBusy(true); setError(''); setNotice('');
    try { await catalogService.publicarMalla(malla.id); await cargarMallas(); setNotice(`Malla ${malla.version} publicada.`); }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo publicar la malla.'); }
    finally { setBusy(false); }
  }

  const items = section === 'carreras' ? carreras : section === 'mallas' ? mallas : section === 'niveles' ? niveles : asignaturas;
  const singular: Record<Section, string> = { carreras: 'carrera', mallas: 'malla', niveles: 'nivel', asignaturas: 'asignatura' };

  return <main className="catalog-page">
    <header className="catalog-header"><div><p className="catalog-eyebrow">HOMOLOGACIONES FISEI · ADMINISTRACIÓN</p><h1>Mantenimiento del catálogo</h1><p>Administra carreras, versiones de malla, niveles y asignaturas.</p></div><Link to="/catalogo" className="catalog-back-link">Volver a consulta</Link></header>
    <nav className="maintenance-tabs" aria-label="Secciones del catálogo">{(['carreras','mallas','niveles','asignaturas'] as Section[]).map(tab => <button type="button" key={tab} className={section === tab ? 'active' : ''} onClick={() => { setSection(tab); setEditId(''); setSearchText(''); setError(''); setNotice(''); }}>{tab[0].toUpperCase()+tab.slice(1)}</button>)}</nav>
    <section className="maintenance-context">
      {section !== 'carreras' && <SearchableSelect key="maintenance-career" id="maintenance-carrera" label="Buscar carrera" value={carreraId} placeholder="Escribe para buscar una carrera" options={carreras.map(c => ({ value: c.id, label: `${c.nombre} (${c.codigo})` }))} onChange={id => { setCarreraId(id); setMallaId(''); setNivelId(''); }} />}
      {(section === 'niveles' || section === 'asignaturas') && <SearchableSelect key={`maintenance-malla-${carreraId}`} id="maintenance-malla" label="Buscar malla" value={mallaId} placeholder="Escribe para buscar una malla" disabled={!carreraId} options={mallas.map(m => ({ value: m.id, label: `Versión ${m.version} · ${m.estado || (m.vigente ? 'Publicada' : 'Borrador')}` }))} onChange={id => { setMallaId(id); setNivelId(''); }} />}
      {section === 'asignaturas' && <SearchableSelect key={`maintenance-nivel-${mallaId}`} id="maintenance-nivel" label="Buscar nivel" value={nivelId} placeholder="Escribe para buscar un nivel" disabled={!mallaId} options={niveles.map(n => ({ value: n.id, label: `Nivel ${n.numero}` }))} onChange={setNivelId} />}
    </section>
    {error && <div role="alert" className="catalog-error">{error}</div>}{notice && <div role="status" className="maintenance-notice">{notice}</div>}
    <section className="maintenance-layout">
      <form className="maintenance-form" onSubmit={submit} key={`${section}-${editId}`}>
        <h2>{editId ? 'Editar' : 'Agregar'} {singular[section]}</h2>
        {section === 'carreras' && <><label>Código<input name="codigo" required maxLength={20} /></label><label>Nombre<input name="nombre" required maxLength={200} /></label><label className="check-field"><input name="esDemo" type="checkbox" /> Marcar como DEMO</label></>}
        {section === 'mallas' && <label>Versión<input name="version" required maxLength={20} placeholder="Ej. 2026" /></label>}
        {section === 'niveles' && <label>Número de nivel<input name="numero" type="number" required min={1} /></label>}
        {section === 'asignaturas' && <><label>Código<input name="codigo" required maxLength={30} /></label><label>Nombre<input name="nombre" required maxLength={200} /></label><div className="maintenance-form-row"><label>Créditos<input name="creditos" type="number" required min="0.01" max="999.99" step="0.01" /></label><label>Horas<input name="horas" type="number" required min={1} /></label></div><label>Unidad organizacional<input name="unidad" maxLength={100} /></label></>}
        <div className="maintenance-actions"><button className="maintenance-primary" disabled={busy || (section !== 'carreras' && !carreraId) || ((section === 'niveles' || section === 'asignaturas') && !mallaId) || (section === 'asignaturas' && !nivelId)}>{busy ? 'Guardando…' : editId ? 'Guardar cambios' : 'Crear'}</button>{editId && <button type="button" onClick={() => setEditId('')}>Cancelar</button>}</div>
      </form>
      <section className="maintenance-list">
        <div className="maintenance-list-head"><h2>{section[0].toUpperCase() + section.slice(1)}</h2><span>{items.length} registros</span></div>
        <label className="maintenance-search">Buscar en {section}<input type="search" value={searchText} onChange={event => setSearchText(event.target.value)} placeholder={`Escribe para filtrar ${section}`} /></label>
        {items.length === 0 ? <p className="catalog-message">No hay registros en esta sección.</p> : (
          <div className="maintenance-items">
            {items.filter(item => Object.values(item).some(value => String(value ?? '').toLocaleLowerCase().includes(searchText.trim().toLocaleLowerCase()))).map(item => {
              let title: string;
              let detail = '';
              if (section === 'carreras') {
                const carrera = item as Carrera;
                title = carrera.nombre;
                detail = `${carrera.codigo}${carrera.esDemo ? ' · DEMO' : ''}`;
              } else if (section === 'mallas') {
                const malla = item as MallaCurricular;
                title = `Malla ${malla.version}`;
                detail = malla.estado || (malla.vigente ? 'Publicada' : 'Borrador');
              } else if (section === 'niveles') {
                title = `Nivel ${(item as Nivel).numero}`;
              } else {
                const asignatura = item as Asignatura;
                title = `${asignatura.codigo} · ${asignatura.nombre}`;
                detail = `${asignatura.creditos} créditos · ${asignatura.horas} horas`;
              }
              const malla = section === 'mallas' ? item as MallaCurricular : undefined;
              return <article key={item.id} className="maintenance-item">
                <div><strong>{title}</strong><small>{detail}</small></div>
                <div className="maintenance-item-actions">
                  {malla && <button type="button" disabled={busy || malla.estado === 'Publicada'} onClick={() => void publicar(malla)}>Publicar</button>}
                  <button type="button" onClick={() => editar(item)}>Editar</button>
                </div>
              </article>;
            })}
          </div>
        )}
      </section>
    </section>
  </main>;
}
