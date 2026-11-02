import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import SearchableSelect from '../../../components/SearchableSelect';
import {
  catalogService,
  type Asignatura,
  type Carrera,
  type MallaCurricular,
  type Nivel,
} from '../../catalog/services/catalogService';
import {
  studentsService,
  type AsignaturaCursada,
  type Estudiante,
  type EstudianteWriteRequest,
} from '../studentsService';

interface CourseDraft extends AsignaturaCursada {
  clientKey: string;
}

function validarCedula(cedula: string): boolean {
  if (!/^\d{10}$/.test(cedula)) return false;
  const suma = [...cedula.slice(0, 9)].reduce((total, caracter, index) => {
    let digito = Number(caracter);
    if (index % 2 === 0) {
      digito *= 2;
      if (digito > 9) digito -= 9;
    }
    return total + digito;
  }, 0);
  return (10 - (suma % 10)) % 10 === Number(cedula[9]);
}

function ocultarCedula(cedula: string): string {
  return cedula.length < 5 ? '••••' : `••••••${cedula.slice(-4)}`;
}

export default function StudentsPage() {
  const [students, setStudents] = useState<Estudiante[]>([]);
  const [careers, setCareers] = useState<Carrera[]>([]);
  const [mallas, setMallas] = useState<MallaCurricular[]>([]);
  const [niveles, setNiveles] = useState<Nivel[]>([]);
  const [subjects, setSubjects] = useState<Asignatura[]>([]);
  const [filter, setFilter] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState('');
  const [cedula, setCedula] = useState('');
  const [nombres, setNombres] = useState('');
  const [apellidos, setApellidos] = useState('');
  const [careerId, setCareerId] = useState('');
  const [nivelActual, setNivelActual] = useState('');
  const [mallaId, setMallaId] = useState('');
  const [nivelId, setNivelId] = useState('');
  const [selectedSubjectId, setSelectedSubjectId] = useState('');
  const [grade, setGrade] = useState('');
  const [period, setPeriod] = useState('');
  const [courses, setCourses] = useState<CourseDraft[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  async function loadStudents() {
    setLoading(true);
    try {
      setStudents(await studentsService.listar());
      setError('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron cargar los estudiantes.');
    } finally { setLoading(false); }
  }

  useEffect(() => {
    void loadStudents();
    catalogService.obtenerCarreras()
      .then(setCareers)
      .catch(err => setError(err instanceof Error ? err.message : 'No se pudieron cargar las carreras.'));
  }, []);

  useEffect(() => {
    setMallas([]); setMallaId(''); setNiveles([]); setNivelId(''); setSubjects([]); setSelectedSubjectId('');
    if (!careerId) return;
    let active = true;
    catalogService.obtenerMallas(careerId).then(rows => { if (active) setMallas(rows); })
      .catch(err => { if (active) setError(err instanceof Error ? err.message : 'No se pudieron cargar las mallas.'); });
    return () => { active = false; };
  }, [careerId]);

  useEffect(() => {
    setNiveles([]); setNivelId(''); setSubjects([]); setSelectedSubjectId('');
    if (!mallaId) return;
    let active = true;
    catalogService.obtenerNiveles(mallaId).then(rows => { if (active) setNiveles(rows); })
      .catch(err => { if (active) setError(err instanceof Error ? err.message : 'No se pudieron cargar los niveles.'); });
    return () => { active = false; };
  }, [mallaId]);

  useEffect(() => {
    setSubjects([]); setSelectedSubjectId('');
    if (!mallaId || !nivelId) return;
    let active = true;
    catalogService.obtenerAsignaturas(mallaId, nivelId, '').then(rows => { if (active) setSubjects(rows); })
      .catch(err => { if (active) setError(err instanceof Error ? err.message : 'No se pudieron cargar las asignaturas.'); });
    return () => { active = false; };
  }, [mallaId, nivelId]);

  function resetForm() {
    setShowForm(false); setEditingId(''); setCedula(''); setNombres(''); setApellidos('');
    setCareerId(''); setNivelActual(''); setMallaId(''); setNivelId(''); setSelectedSubjectId('');
    setGrade(''); setPeriod(''); setCourses([]); setError(''); setNotice('');
  }

  function beginCreate() {
    resetForm();
    setShowForm(true);
  }

  function beginEdit(student: Estudiante) {
    if (student.tipo !== 0 || !student.historial) {
      setError('Esta pantalla permite editar estudiantes internos con historial académico.');
      return;
    }
    setShowForm(true); setEditingId(student.id); setCedula(student.cedula);
    setNombres(student.nombres); setApellidos(student.apellidos);
    setCareerId(student.historial.carreraOrigenId ?? '');
    setNivelActual(student.historial.nivelActual == null ? '' : String(student.historial.nivelActual));
    setCourses(student.historial.asignaturas.map((course, index) => ({ ...course, clientKey: course.id ?? `${course.asignaturaId}-${index}` })));
    setError(''); setNotice('');
  }

  function addCourse() {
    const subject = subjects.find(item => item.id === selectedSubjectId);
    if (!subject) { setError('Selecciona una asignatura del catálogo.'); return; }
    if (courses.some(course => course.asignaturaId === subject.id)) { setError('Esa asignatura ya está en el historial.'); return; }
    const nota = Number(grade);
    if (grade === '' || nota < 0 || nota > 10) { setError('La nota debe estar entre 0 y 10.'); return; }
    if (!period.trim()) { setError('El período es obligatorio.'); return; }
    setCourses(current => [...current, {
      clientKey: `${subject.id}-${Date.now()}`,
      asignaturaId: subject.id,
      codigo: subject.codigo,
      nombre: subject.nombre,
      creditos: subject.creditos,
      horas: subject.horas,
      nota,
      periodo: period.trim(),
    }]);
    setSelectedSubjectId(''); setGrade(''); setPeriod(''); setError('');
  }

  function updateCourse(clientKey: string, field: 'nota' | 'periodo', value: string) {
    setCourses(current => current.map(course => course.clientKey === clientKey
      ? { ...course, [field]: field === 'nota' ? Number(value) : value }
      : course));
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setSaving(true); setError(''); setNotice('');
    const normalizedCedula = cedula.trim();
    if (!validarCedula(normalizedCedula)) {
      setError('Ingresa una cédula ecuatoriana válida de 10 dígitos.'); setSaving(false); return;
    }
    if (!careerId || !nivelActual || Number(nivelActual) <= 0) {
      setError('Selecciona la carrera de origen y el nivel actual.'); setSaving(false); return;
    }
    if (courses.length === 0) {
      setError('Agrega al menos una asignatura aprobada al historial.'); setSaving(false); return;
    }
    if (courses.some(course => !course.periodo.trim() || !Number.isFinite(course.nota) || course.nota < 0 || course.nota > 10)) {
      setError('Revisa las notas y períodos de las asignaturas del historial.'); setSaving(false); return;
    }

    const payload: EstudianteWriteRequest = {
      cedula: normalizedCedula,
      tipoIdentificacion: 0,
      nombres: nombres.trim(),
      apellidos: apellidos.trim(),
      tipo: 0,
      historial: {
        carreraOrigenId: careerId,
        carreraOrigenNombre: null,
        institucionOrigen: null,
        nivelActual: Number(nivelActual),
        asignaturas: courses.map(({ clientKey: _clientKey, id: _id, ...course }) => course),
      },
    };

    try {
      if (editingId) await studentsService.actualizar(editingId, payload);
      else await studentsService.crear(payload);
      setNotice(editingId ? 'Estudiante actualizado correctamente.' : 'Estudiante registrado correctamente.');
      setShowForm(false); setEditingId('');
      await loadStudents();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar el estudiante.');
    } finally { setSaving(false); }
  }

  const filteredStudents = useMemo(() => {
    const term = filter.trim().toLocaleLowerCase();
    if (!term) return students;
    return students.filter(student => `${student.cedula} ${student.nombres} ${student.apellidos} ${student.apellidos} ${student.nombres}`.toLocaleLowerCase().includes(term));
  }, [filter, students]);

  const selectedSubject = subjects.find(subject => subject.id === selectedSubjectId);

  return <main className="catalog-page students-page">
    <header className="catalog-header">
      <div><p className="catalog-eyebrow">HOMOLOGACIONES FISEI · EXPEDIENTES ACADÉMICOS</p><h1>Estudiantes</h1><p>Registra estudiantes internos y administra sus asignaturas aprobadas.</p></div>
      <Link to="/home" className="catalog-back-link">Volver al inicio</Link>
    </header>

    {notice && <div role="status" className="maintenance-notice">{notice}</div>}
    {error && <div role="alert" className="catalog-error">{error}</div>}

    <section className="students-panel">
      <div className="students-panel-heading"><div><h2>Registro y listado</h2><p>{students.length} estudiantes registrados</p></div>
        {!showForm && <button type="button" className="students-primary" onClick={beginCreate}>Registrar estudiante</button>}
      </div>

      {showForm && <form className="student-form" onSubmit={submit}>
        <div className="student-form-heading"><div><h3>{editingId ? 'Editar estudiante interno' : 'Registrar estudiante interno'}</h3><p>Completa los datos y agrega las asignaturas aprobadas.</p></div></div>
        <div className="student-form-grid">
          <label>Cédula<input inputMode="numeric" value={cedula} onChange={event => setCedula(event.target.value.replace(/\D/g, '').slice(0, 10))} required minLength={10} maxLength={10} placeholder="10 dígitos" /></label>
          <label>Nombres<input value={nombres} onChange={event => setNombres(event.target.value)} required maxLength={150} /></label>
          <label>Apellidos<input value={apellidos} onChange={event => setApellidos(event.target.value)} required maxLength={150} /></label>
          <SearchableSelect id="student-career" label="Carrera de origen" value={careerId} placeholder="Escribe para buscar la carrera" options={careers.map(career => ({ value: career.id, label: `${career.nombre} · ${career.codigo}` }))} onChange={setCareerId} />
          <label>Nivel actual<input type="number" min={1} step={1} value={nivelActual} onChange={event => setNivelActual(event.target.value)} required /></label>
        </div>

        <fieldset className="student-course-picker" disabled={!careerId}>
          <legend>Asignaturas cursadas aprobadas</legend>
          <div className="student-course-picker-grid">
            <SearchableSelect key={`student-malla-${careerId}`} id="student-malla" label="Malla" value={mallaId} placeholder="Escribe para buscar una malla" disabled={!careerId || mallas.length === 0} options={mallas.map(malla => ({ value: malla.id, label: `Versión ${malla.version}${malla.vigente ? ' · vigente' : ''}` }))} onChange={setMallaId} />
            <SearchableSelect key={`student-level-${mallaId}`} id="student-level" label="Nivel" value={nivelId} placeholder="Escribe para buscar un nivel" disabled={!mallaId || niveles.length === 0} options={niveles.map(level => ({ value: level.id, label: `Nivel ${level.numero}` }))} onChange={setNivelId} />
            <SearchableSelect key={`student-subject-${nivelId}`} id="student-subject" label="Asignatura" value={selectedSubjectId} placeholder="Escribe para buscar una asignatura" disabled={!nivelId} options={subjects.map(subject => ({ value: subject.id, label: `${subject.codigo} · ${subject.nombre}` }))} onChange={setSelectedSubjectId} />
            <label>Nota<input type="number" min="0" max="10" step="0.01" value={grade} onChange={event => setGrade(event.target.value)} placeholder="0–10" /></label>
            <label>Período<input value={period} onChange={event => setPeriod(event.target.value)} maxLength={30} placeholder="Ej. 2026-A" /></label>
            <button type="button" className="students-secondary" onClick={addCourse} disabled={!selectedSubject || !grade || !period.trim()}>Agregar asignatura</button>
          </div>
          {selectedSubject && <p className="student-course-meta">{selectedSubject.creditos} créditos · {selectedSubject.horas} horas</p>}
        </fieldset>

        {courses.length > 0 && <div className="student-course-list"><h4>Historial agregado ({courses.length})</h4><div className="student-course-table-wrap"><table className="student-course-table"><thead><tr><th>Asignatura</th><th>Créditos</th><th>Horas</th><th>Nota</th><th>Período</th><th></th></tr></thead><tbody>
          {courses.map(course => <tr key={course.clientKey}><td><strong>{course.codigo}</strong><br />{course.nombre}</td><td>{course.creditos ?? '—'}</td><td>{course.horas ?? '—'}</td><td><input aria-label={`Nota de ${course.nombre}`} type="number" min="0" max="10" step="0.01" value={course.nota} onChange={event => updateCourse(course.clientKey, 'nota', event.target.value)} /></td><td><input aria-label={`Período de ${course.nombre}`} value={course.periodo} maxLength={30} onChange={event => updateCourse(course.clientKey, 'periodo', event.target.value)} /></td><td><button type="button" className="students-remove" onClick={() => setCourses(current => current.filter(item => item.clientKey !== course.clientKey))}>Quitar</button></td></tr>)}
        </tbody></table></div></div>}

        <div className="student-form-actions"><button className="students-primary" disabled={saving}>{saving ? 'Guardando…' : editingId ? 'Guardar cambios' : 'Registrar estudiante'}</button><button type="button" className="students-secondary" onClick={resetForm} disabled={saving}>Cancelar</button></div>
      </form>}

      <div className="students-list-toolbar"><label>Buscar por cédula o nombre<input type="search" value={filter} onChange={event => setFilter(event.target.value)} placeholder="Escribe un nombre o cédula" /></label><span>{filteredStudents.length} resultados</span></div>
      {loading ? <p className="catalog-message" role="status">Cargando estudiantes…</p> : filteredStudents.length === 0 ? <p className="catalog-message">{filter ? 'No hay estudiantes que coincidan con la búsqueda.' : 'Aún no hay estudiantes registrados.'}</p> : (
        <div className="student-table-wrap"><table className="student-table"><thead><tr><th>Estudiante</th><th>Identificación</th><th>Tipo</th><th>Carrera de origen</th><th>Nivel</th><th>Asignaturas</th><th>Acciones</th></tr></thead><tbody>
          {filteredStudents.map(student => {
            const history = student.historial;
            const careerName = careers.find(career => career.id === history?.carreraOrigenId)?.nombre ?? history?.carreraOrigenNombre ?? '—';
            return <tr key={student.id}><td><strong>{student.apellidos}, {student.nombres}</strong></td><td>{ocultarCedula(student.cedula)}</td><td>{student.tipo === 0 ? 'Interno' : 'Externo'}</td><td>{careerName}</td><td>{history?.nivelActual ?? '—'}</td><td>{history?.asignaturas.length ?? 0}</td><td>{student.tipo === 0 ? <button type="button" className="students-edit" onClick={() => beginEdit(student)}>Editar</button> : <span className="student-readonly-note">Se gestiona en importaciones</span>}</td></tr>;
          })}
        </tbody></table></div>
      )}
    </section>
  </main>;
}
