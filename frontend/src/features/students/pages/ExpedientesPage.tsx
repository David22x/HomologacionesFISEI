import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import SearchableSelect from '../../../components/SearchableSelect';
import {
  catalogService,
  type Carrera,
  type MallaCurricular,
} from '../../catalog/services/catalogService';
import {
  expedientesService,
  estadosExpediente,
  type Expediente,
} from '../expedientesService';
import { studentsService, type Estudiante } from '../studentsService';

interface ExpedientesPageProps {
  canCreate: boolean;
}

export default function ExpedientesPage({ canCreate }: ExpedientesPageProps) {
  const [students, setStudents] = useState<Estudiante[]>([]);
  const [careers, setCareers] = useState<Carrera[]>([]);
  const [mallas, setMallas] = useState<MallaCurricular[]>([]);
  const [detailMallas, setDetailMallas] = useState<MallaCurricular[]>([]);
  const [expedientes, setExpedientes] = useState<Expediente[]>([]);
  const [selectedExpediente, setSelectedExpediente] = useState<Expediente | null>(null);
  const [studentId, setStudentId] = useState('');
  const [careerId, setCareerId] = useState('');
  const [mallaId, setMallaId] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadingMallas, setLoadingMallas] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const detailRequestId = useRef(0);

  useEffect(() => {
    let active = true;
    Promise.all([
      studentsService.listar(),
      catalogService.obtenerCarreras(),
      expedientesService.listar(),
    ]).then(([studentRows, careerRows, expedienteRows]) => {
      if (!active) return;
      setStudents(studentRows);
      setCareers(careerRows);
      setExpedientes(expedienteRows);
      setError('');
    }).catch(cause => {
      if (active) setError(cause instanceof Error ? cause.message : 'No se pudieron cargar los expedientes.');
    }).finally(() => {
      if (active) setLoading(false);
    });

    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!careerId) return;
    let active = true;
    catalogService.obtenerMallas(careerId).then(rows => {
      if (active) setMallas(rows);
    }).catch(cause => {
      if (active) setError(cause instanceof Error ? cause.message : 'No se pudieron cargar las mallas.');
    }).finally(() => {
      if (active) setLoadingMallas(false);
    });
    return () => { active = false; };
  }, [careerId]);

  function changeCareer(value: string) {
    setCareerId(value);
    setMallas([]);
    setMallaId('');
    setLoadingMallas(Boolean(value));
  }

  async function selectExpediente(expediente: Expediente) {
    const requestId = ++detailRequestId.current;
    setSelectedExpediente(expediente);
    setDetailMallas([]);
    setError('');
    try {
      const rows = await catalogService.obtenerMallas(expediente.carreraDestinoId);
      if (requestId === detailRequestId.current) setDetailMallas(rows);
    } catch (cause) {
      if (requestId === detailRequestId.current) {
        setError(cause instanceof Error ? cause.message : 'No se pudo cargar la malla del expediente.');
      }
    }
  }

  async function createExpediente(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!studentId || !careerId || !mallaId) {
      setError('Selecciona un estudiante, una carrera y una malla.');
      return;
    }

    setSaving(true);
    setError('');
    setNotice('');
    try {
      const created = await expedientesService.crear({
        estudianteRefId: studentId,
        carreraDestinoRefId: careerId,
        mallaAplicableRefId: mallaId,
      });
      setExpedientes(current => [created, ...current]);
      await selectExpediente(created);
      setStudentId('');
      changeCareer('');
      setNotice('El expediente se creó correctamente.');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo crear el expediente.');
    } finally {
      setSaving(false);
    }
  }

  const selectedStudent = students.find(student => student.id === selectedExpediente?.estudianteId);
  const selectedCareer = careers.find(career => career.id === selectedExpediente?.carreraDestinoId);
  const selectedMalla = detailMallas.find(malla => malla.id === selectedExpediente?.mallaDestinoId);

  return <main className="catalog-page expediente-page">
    <header className="catalog-header">
      <div>
        <p className="catalog-eyebrow">HOMOLOGACIONES FISEI · EXPEDIENTES ACADÉMICOS</p>
        <h1>Expedientes</h1>
        <p>Crea expedientes de homologación y consulta su estado actual.</p>
      </div>
      <div className="students-header-links">
        <Link to="/estudiantes" className="catalog-back-link">Volver a estudiantes</Link>
        <Link to="/home" className="catalog-back-link">Volver al inicio</Link>
      </div>
    </header>

    {notice && <p className="maintenance-notice" role="status">{notice}</p>}
    {error && <p className="catalog-error" role="alert">{error}</p>}

    {canCreate && <section className="students-panel expediente-create" aria-labelledby="create-expediente-heading">
      <div className="students-panel-heading">
        <div>
          <h2 id="create-expediente-heading">Crear expediente</h2>
          <p>Selecciona al estudiante y el plan de estudios para iniciar la homologación.</p>
        </div>
      </div>
      <form className="expediente-form" onSubmit={createExpediente}>
        <SearchableSelect
          id="expediente-student"
          label="Estudiante"
          value={studentId}
          placeholder="Busca por nombre o identificación"
          options={students.map(student => ({
            value: student.id,
            label: `${student.apellidos}, ${student.nombres} · ${student.cedula}`,
          }))}
          onChange={setStudentId}
        />
        <SearchableSelect
          id="expediente-career"
          label="Carrera de destino"
          value={careerId}
          placeholder="Busca una carrera"
          options={careers.map(career => ({ value: career.id, label: `${career.nombre} · ${career.codigo}` }))}
          onChange={changeCareer}
        />
        <SearchableSelect
          id="expediente-malla"
          label="Malla curricular"
          value={mallaId}
          placeholder="Busca una malla"
          disabled={!careerId || mallas.length === 0}
          options={mallas.map(malla => ({
            value: malla.id,
            label: `Versión ${malla.version}${malla.vigente ? ' · vigente' : ''}`,
          }))}
          onChange={setMallaId}
        />
        <p className="expediente-initial-state">Estado inicial: <strong>Borrador</strong></p>
        <button className="students-primary" type="submit" disabled={saving || loading || students.length === 0 || !careerId || !mallaId}>
          {saving ? 'Creando…' : 'Crear expediente'}
        </button>
      </form>
      {!loading && students.length === 0 && <p className="catalog-message">Registra al menos un estudiante antes de crear un expediente.</p>}
      {careerId && loadingMallas && <p className="catalog-message" role="status">Cargando mallas…</p>}
      {careerId && !loadingMallas && mallas.length === 0 && <p className="catalog-message">Esta carrera no tiene mallas curriculares disponibles.</p>}
    </section>}

    <section className="students-panel expediente-list" aria-labelledby="expediente-list-heading">
      <div className="students-panel-heading">
        <div>
          <h2 id="expediente-list-heading">Expedientes registrados</h2>
          <p>{expedientes.length} expedientes</p>
        </div>
      </div>
      {loading ? <p className="catalog-message" role="status">Cargando expedientes…</p>
        : expedientes.length === 0 ? <p className="catalog-message">Aún no hay expedientes registrados.</p>
          : <div className="student-table-wrap">
            <table className="student-table">
              <thead><tr><th>Estudiante</th><th>Carrera</th><th>Creado</th><th>Estado</th><th></th></tr></thead>
              <tbody>{expedientes.map(expediente => {
                const student = students.find(item => item.id === expediente.estudianteId);
                const career = careers.find(item => item.id === expediente.carreraDestinoId);
                return <tr key={expediente.id}>
                  <td>{student ? <strong>{student.apellidos}, {student.nombres}</strong> : expediente.estudianteId}</td>
                  <td>{career?.nombre ?? expediente.carreraDestinoId}</td>
                  <td>{new Date(expediente.creadoEn).toLocaleDateString()}</td>
                  <td><span className={`expediente-status status-${estadoClass(expediente.estado)}`}>{estadoLabel(expediente.estado)}</span></td>
                  <td><button className="students-secondary" type="button" onClick={() => { void selectExpediente(expediente); }}>Ver expediente</button></td>
                </tr>;
              })}</tbody>
            </table>
          </div>}
    </section>

    {selectedExpediente && <section className="students-panel expediente-detail" aria-labelledby="expediente-detail-heading">
      <div className="students-panel-heading">
        <div>
          <h2 id="expediente-detail-heading">Detalle del expediente</h2>
          <p>Expediente {selectedExpediente.id}</p>
        </div>
        <span className={`expediente-status status-${estadoClass(selectedExpediente.estado)}`}>{estadoLabel(selectedExpediente.estado)}</span>
      </div>
      <dl className="expediente-details">
        <div><dt>Estudiante</dt><dd>{selectedStudent ? `${selectedStudent.apellidos}, ${selectedStudent.nombres}` : selectedExpediente.estudianteId}</dd></div>
        <div><dt>Identificación</dt><dd>{selectedStudent?.cedula ?? '—'}</dd></div>
        <div><dt>Carrera de destino</dt><dd>{selectedCareer?.nombre ?? selectedExpediente.carreraDestinoId}</dd></div>
        <div><dt>Malla curricular</dt><dd>{selectedMalla ? `Versión ${selectedMalla.version}` : selectedExpediente.mallaDestinoId}</dd></div>
        <div><dt>Profesor responsable</dt><dd>{selectedExpediente.profesorNombre || selectedExpediente.profesorId}</dd></div>
        <div><dt>Fecha de creación</dt><dd>{new Date(selectedExpediente.creadoEn).toLocaleString()}</dd></div>
      </dl>
    </section>}
  </main>;
}

function estadoLabel(estado: number | string): string {
  if (typeof estado === 'number') {
    return estadosNumericos[estado] ?? `Estado no reconocido (${estado})`;
  }
  if (estado === 'EnRevision') return 'En revisión';
  return estadosExpediente.find(value => value === estado) ?? `Estado no reconocido (${estado})`;
}

const estadosNumericos: Record<number, (typeof estadosExpediente)[number]> = {
  0: 'Borrador',
  1: 'En revisión',
  2: 'Finalizado',
  3: 'Anulado',
};

function estadoClass(estado: number | string): string {
  return estadoLabel(estado).toLocaleLowerCase().replaceAll(' ', '-').replaceAll('ó', 'o');
}
