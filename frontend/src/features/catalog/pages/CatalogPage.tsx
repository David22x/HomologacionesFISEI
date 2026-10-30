
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { catalogService } from '../services/catalogService';
import type {
  Asignatura,
  Carrera,
  MallaCurricular,
  Nivel,
} from '../services/catalogService';
import SearchableSelect from '../../../components/SearchableSelect';

export default function CatalogPage() {
  const [carreras, setCarreras] = useState<Carrera[]>([]);
  const [mallas, setMallas] = useState<MallaCurricular[]>([]);
  const [niveles, setNiveles] = useState<Nivel[]>([]);
  const [asignaturas, setAsignaturas] = useState<Asignatura[]>([]);

  const [carreraId, setCarreraId] = useState('');
  const [mallaId, setMallaId] = useState('');
  const [nivelId, setNivelId] = useState('');
  const [busqueda, setBusqueda] = useState('');

  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let activo = true;

    async function cargarCarreras() {
      setCargando(true);
      setError('');

      try {
        const datos = await catalogService.obtenerCarreras();
        if (activo) setCarreras(datos);
      } catch (err) {
        if (activo) {
          setError(
            err instanceof Error
              ? err.message
              : 'No se pudieron cargar las carreras.',
          );
        }
      } finally {
        if (activo) setCargando(false);
      }
    }

    void cargarCarreras();

    return () => {
      activo = false;
    };
  }, []);

  useEffect(() => {
    let activo = true;

    setMallas([]);
    setMallaId('');
    setNiveles([]);
    setNivelId('');
    setAsignaturas([]);
    setBusqueda('');

    if (!carreraId) return;

    async function cargarMallas() {
      setCargando(true);
      setError('');

      try {
        const datos = await catalogService.obtenerMallas(carreraId);
        if (activo) setMallas(datos);
      } catch (err) {
        if (activo) {
          setError(
            err instanceof Error
              ? err.message
              : 'No se pudieron cargar las mallas.',
          );
        }
      } finally {
        if (activo) setCargando(false);
      }
    }

    void cargarMallas();

    return () => {
      activo = false;
    };
  }, [carreraId]);

  useEffect(() => {
    let activo = true;

    setNiveles([]);
    setNivelId('');
    setAsignaturas([]);
    setBusqueda('');

    if (!mallaId) return;

    async function cargarNiveles() {
      setCargando(true);
      setError('');

      try {
        const datos = await catalogService.obtenerNiveles(mallaId);
        if (activo) setNiveles(datos);
      } catch (err) {
        if (activo) {
          setError(
            err instanceof Error
              ? err.message
              : 'No se pudieron cargar los niveles.',
          );
        }
      } finally {
        if (activo) setCargando(false);
      }
    }

    void cargarNiveles();

    return () => {
      activo = false;
    };
  }, [mallaId]);

  useEffect(() => {
    let activo = true;

    setAsignaturas([]);

    if (!mallaId || !nivelId) return;

    async function cargarAsignaturas() {
      setCargando(true);
      setError('');

      try {
        const datos = await catalogService.obtenerAsignaturas(
          mallaId,
          nivelId,
          busqueda,
        );

        if (activo) setAsignaturas(datos);
      } catch (err) {
        if (activo) {
          setError(
            err instanceof Error
              ? err.message
              : 'No se pudieron cargar las asignaturas.',
          );
        }
      } finally {
        if (activo) setCargando(false);
      }
    }

    void cargarAsignaturas();

    return () => {
      activo = false;
    };
  }, [mallaId, nivelId, busqueda]);

  const carreraSeleccionada = carreras.find(
    (carrera) => carrera.id === carreraId,
  );

  const mallaSeleccionada = mallas.find(
    (malla) => malla.id === mallaId,
  );

  const nivelSeleccionado = niveles.find(
    (nivel) => nivel.id === nivelId,
  );

  return (
    <main className="catalog-page">
      <header className="catalog-header">
        <div>
          <p className="catalog-eyebrow">HOMOLOGACIONES FISEI</p>
          <h1>Catálogo académico</h1>
          <p>
            Consulta las asignaturas por carrera, malla curricular y nivel.
          </p>
        </div>

        <Link to="/home" className="catalog-back-link">
          Volver al inicio
        </Link>
      </header>

      <section className="catalog-filters" aria-label="Filtros del catálogo">
        <SearchableSelect id="carrera" label="Carrera" value={carreraId} placeholder="Escribe para buscar una carrera" options={carreras.map(carrera => ({ value: carrera.id, label: `${carrera.nombre}${carrera.esDemo ? ' (DEMO)' : ''} · ${carrera.codigo}` }))} onChange={setCarreraId} />
        <SearchableSelect key={`catalogo-malla-${carreraId}`} id="malla" label="Malla curricular" value={mallaId} placeholder="Escribe para buscar una malla" disabled={!carreraId || mallas.length === 0} options={mallas.map(malla => ({ value: malla.id, label: `Versión ${malla.version}${malla.vigente ? ' (vigente)' : ''}` }))} onChange={setMallaId} />
        <SearchableSelect key={`catalogo-nivel-${mallaId}`} id="nivel" label="Nivel" value={nivelId} placeholder="Escribe para buscar un nivel" disabled={!mallaId || niveles.length === 0} options={niveles.map(nivel => ({ value: nivel.id, label: `Nivel ${nivel.numero}` }))} onChange={setNivelId} />
      </section>

      {carreraSeleccionada && mallaSeleccionada && nivelSeleccionado && (
        <p className="catalog-selection">
          {carreraSeleccionada.nombre} · Malla {mallaSeleccionada.version}
          {' · '}Nivel {nivelSeleccionado.numero}
        </p>
      )}

      {error && (
        <div className="catalog-error" role="alert">
          {error}
        </div>
      )}

      <section className="catalog-results">
        <div className="catalog-results-header">
          <div>
            <h2>Asignaturas</h2>
            <p>Resultados del catálogo académico.</p>
          </div>

          <span className="catalog-count">
            {asignaturas.length} asignaturas
          </span>
        </div>

        <div className="catalog-search">
          <label htmlFor="busqueda">Buscar por código o nombre</label>
          <input
            id="busqueda"
            type="search"
            placeholder="Ej. programación..."
            value={busqueda}
            onChange={(event) => setBusqueda(event.target.value)}
            disabled={!mallaId || !nivelId}
          />
        </div>

        {cargando ? (
          <p className="catalog-message" role="status">
            Cargando información del catálogo...
          </p>
        ) : carreraSeleccionada?.esDemo ? (
          <p className="catalog-message">
            Esta carrera está marcada como DEMO y no tiene mallas ni asignaturas
            precargadas.
          </p>
        ) : !carreraId || !mallaId || !nivelId ? (
          <p className="catalog-message">
            Selecciona una carrera, una malla y un nivel para consultar
            las asignaturas.
          </p>
        ) : error ? (
          <p className="catalog-message">
            No se pudieron mostrar las asignaturas.
          </p>
        ) : asignaturas.length === 0 ? (
          <p className="catalog-message">
            No se encontraron asignaturas para los filtros seleccionados.
          </p>
        ) : (
          <div className="catalog-table-wrapper">
            <table className="catalog-table">
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Asignatura</th>
                  <th>Nivel</th>
                  <th>Créditos</th>
                  <th>Horas</th>
                  <th>Unidad organizacional</th>
                  <th>Unidades temáticas</th>
                </tr>
              </thead>
              <tbody>
                {asignaturas.map((asignatura) => (
                  <tr key={asignatura.id}>
                    <td>{asignatura.codigo}</td>
                    <td>{asignatura.nombre}</td>
                    <td>{asignatura.nivelNumero}</td>
                    <td>{asignatura.creditos}</td>
                    <td>{asignatura.horas}</td>
                    <td>{asignatura.unidadOrganizacional ?? '—'}</td>
                    <td><Link className="catalog-topic-link" to={`/catalogo/asignaturas/${asignatura.id}/temas`} state={{ asignatura }}>Ver temas</Link></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}
