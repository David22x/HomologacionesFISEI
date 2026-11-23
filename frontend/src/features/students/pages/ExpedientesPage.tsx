import { useEffect, useRef, useState, type FormEvent } from 'react';
import SearchableSelect from '../../../components/SearchableSelect';
import {
  catalogService,
  type Asignatura,
  type Carrera,
  type MallaCurricular,
} from '../../catalog/services/catalogService';
import {
  expedientesService,
  estadosExpediente,
  type AnalisisExpedienteIa,
  type DecisionExpediente,
  type Expediente,
  type ParHomologacion,
  type PropuestaExpedienteIa,
  type ResultadoParExpediente,
} from '../expedientesService';
import { studentsService, type AsignaturaCursada, type Estudiante } from '../studentsService';

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
  const [originCourses, setOriginCourses] = useState<AsignaturaCursada[]>([]);
  const [destinationCourses, setDestinationCourses] = useState<Asignatura[]>([]);
  const [pairs, setPairs] = useState<ParHomologacion[]>([]);
  const [results, setResults] = useState<ResultadoParExpediente[]>([]);
  const [iaAnalysis, setIaAnalysis] = useState<AnalisisExpedienteIa | null>(null);
  const [decisions, setDecisions] = useState<DecisionExpediente[]>([]);
  const [analyzingIa, setAnalyzingIa] = useState(false);
  const [iaError, setIaError] = useState('');
  const [reviewFormKey, setReviewFormKey] = useState<string | null>(null);
  const [reviewMode, setReviewMode] = useState<'accept' | 'custom' | null>(null);
  const [reviewOutcome, setReviewOutcome] = useState('');
  const [reviewComment, setReviewComment] = useState('');
  const [recommendHomologar, setRecommendHomologar] = useState(false);
  const [savingReviewKey, setSavingReviewKey] = useState<string | null>(null);
  const [groupFormKey, setGroupFormKey] = useState<string | null>(null);
  const [groupOriginIds, setGroupOriginIds] = useState<string[]>([]);
  const [grouping, setGrouping] = useState(false);
  const [resolving, setResolving] = useState(false);
  const [pairOriginId, setPairOriginId] = useState('');
  const [pairDestinationId, setPairDestinationId] = useState('');
  const [studentId, setStudentId] = useState('');
  const [careerId, setCareerId] = useState('');
  const [mallaId, setMallaId] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadingMallas, setLoadingMallas] = useState(false);
  const [saving, setSaving] = useState(false);
  const [evaluating, setEvaluating] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
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
    setAnalyzingIa(false);
    setGrouping(false);
    setResolving(false);
    setSavingReviewKey(null);
    setDetailMallas([]);
    setOriginCourses([]);
    setDestinationCourses([]);
    setPairs([]);
    setResults([]);
    setIaAnalysis(null);
    setDecisions([]);
    setIaError('');
    setReviewFormKey(null);
    setReviewMode(null);
    setReviewOutcome('');
    setReviewComment('');
    setRecommendHomologar(false);
    setGroupFormKey(null);
    setGroupOriginIds([]);
    setPairOriginId('');
    setPairDestinationId('');
    setLoadingDetail(true);
    setError('');
    try {
      const student = students.find(item => item.id === expediente.estudianteId);
      const [rows, pairRows, analisisGuardado, decisionRows] = await Promise.all([
        catalogService.obtenerMallas(expediente.carreraDestinoId),
        expedientesService.listarPares(expediente.id),
        expedientesService.consultarAnalisis(expediente.id),
        expedientesService.listarDecisiones(expediente.id),
      ]);
      const levels = await catalogService.obtenerNiveles(expediente.mallaDestinoId);
      const assignmentGroups = await Promise.all(levels.map(level =>
        catalogService.obtenerAsignaturas(expediente.mallaDestinoId, level.id, ''),
      ));
      if (requestId === detailRequestId.current) {
        setDetailMallas(rows);
        setOriginCourses(student?.historial?.asignaturas ?? []);
        setDestinationCourses(assignmentGroups.flat());
        setPairs(pairRows);
        setIaAnalysis(analisisGuardado);
        setDecisions(decisionRows);
      }
    } catch (cause) {
      if (requestId === detailRequestId.current) {
        setError(cause instanceof Error ? cause.message : 'No se pudo cargar la malla del expediente.');
      }
    } finally {
      if (requestId === detailRequestId.current) setLoadingDetail(false);
    }
  }

  function addPair() {
    if (!selectedExpediente || !pairOriginId || !pairDestinationId) return;
    if (pairs.some(pair => pair.asignaturaCursadaId === pairOriginId
      && pair.asignaturaDestinoId === pairDestinationId)) {
      setError('Ese par ya está agregado.');
      return;
    }
    setPairs(current => [...current, {
      id: `${pairOriginId}:${pairDestinationId}`,
      expedienteId: selectedExpediente.id,
      asignaturaCursadaId: pairOriginId,
      asignaturaDestinoId: pairDestinationId,
      precargado: false,
    }]);
    setResults([]);
    setPairOriginId('');
    setPairDestinationId('');
    setError('');
  }

  async function preloadPairs() {
    if (!selectedExpediente) return;
    setSaving(true);
    setError('');
    setNotice('');
    try {
      const rows = await expedientesService.precargarPares(selectedExpediente.id);
      const keys = new Set(rows.map(pair => `${pair.asignaturaCursadaId}:${pair.asignaturaDestinoId}`));
      const manualesPendientes = pairs.filter(pair => {
        const key = `${pair.asignaturaCursadaId}:${pair.asignaturaDestinoId}`;
        if (keys.has(key)) return false;
        keys.add(key);
        return true;
      });
      setPairs([...rows, ...manualesPendientes]);
      setResults([]);
      setNotice('Se precargaron los pares cuyos nombres coinciden exactamente.');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudieron precargar los pares.');
    } finally {
      setSaving(false);
    }
  }

  async function evaluatePairs() {
    if (!selectedExpediente) return;
    if (pairs.length === 0) {
      setError('Agrega al menos un par antes de evaluar.');
      return;
    }
    setEvaluating(true);
    setError('');
    setNotice('');
    try {
      const savedPairs = await expedientesService.guardarPares(selectedExpediente.id,
        pairs.map(pair => ({
          asignaturaCursadaId: pair.asignaturaCursadaId,
          asignaturaDestinoId: pair.asignaturaDestinoId,
        })),
      );
      setPairs(savedPairs);
      const execution = await expedientesService.evaluarPorReglas(selectedExpediente.id);
      setResults(execution.resultados);
      setSelectedExpediente(current => current
        ? { ...current, estado: execution.estado }
        : current);
      setExpedientes(current => current.map(item => item.id === selectedExpediente.id
        ? { ...item, estado: execution.estado }
        : item));
      setNotice(`Evaluación completada con la versión de reglas ${execution.resultados[0]?.evaluacion.reglas.versionReglas ?? 'vigente'}.`);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No se pudo evaluar el expediente.');
    } finally {
      setEvaluating(false);
    }
  }

  async function analyzeWithIa() {
    if (!selectedExpediente) return;
    const expedienteId = selectedExpediente.id;
    const requestId = detailRequestId.current;
    setAnalyzingIa(true);
    setIaError('');
    setNotice('');
    try {
      const analysis = await expedientesService.analizarConIa(expedienteId);
      if (requestId !== detailRequestId.current) return;
      setIaAnalysis(analysis);
      setSelectedExpediente(current => current
        ? { ...current, estado: analysis.estado }
        : current);
      setExpedientes(current => current.map(item => item.id === expedienteId
        ? { ...item, estado: analysis.estado }
        : item));
      setNotice('El análisis con IA se completó. Las propuestas quedaron guardadas en el expediente.');
    } catch (cause) {
      if (requestId !== detailRequestId.current) return;
      const detail = cause instanceof Error ? cause.message : 'Error inesperado.';
      setIaError(`No se pudo completar el análisis con IA. Verifica que Agent IA y Ollama estén disponibles y vuelve a intentarlo. ${detail}`);
    } finally {
      if (requestId === detailRequestId.current) setAnalyzingIa(false);
    }
  }

  function openOwnConclusion(proposal: PropuestaExpedienteIa) {
    setReviewFormKey(proposalReviewKey(proposal));
    setReviewMode('custom');
    setGroupFormKey(null);
    setReviewOutcome('');
    setReviewComment('');
    setRecommendHomologar(false);
    setIaError('');
  }

  function acceptIaProposal(proposal: PropuestaExpedienteIa) {
    const calculated = Number(proposal.evaluacion.reglas.resultado);
    if (calculated === 0) {
      setGroupFormKey(null);
      setReviewFormKey(null);
      setReviewMode(null);
      void saveProposalReview(proposal, true, '', false);
      return;
    }
    setReviewFormKey(proposalReviewKey(proposal));
    setReviewMode('accept');
    setGroupFormKey(null);
    setReviewOutcome('');
    setReviewComment('');
    setRecommendHomologar(false);
    setIaError('');
  }

  function toggleGroupOrigin(originId: string) {
    setGroupOriginIds(current => current.includes(originId)
      ? current.filter(id => id !== originId)
      : [...current, originId]);
  }

  async function createGroupedProposal(proposal: PropuestaExpedienteIa) {
    if (!selectedExpediente) return;
    if (groupOriginIds.length < 2) {
      setIaError('Selecciona al menos dos asignaturas de origen para crear una propuesta agrupada.');
      return;
    }
    const expedienteId = selectedExpediente.id;
    const requestId = detailRequestId.current;
    setGrouping(true);
    setIaError('');
    setNotice('');
    try {
      const grouped = await expedientesService.agruparOrigenes(expedienteId, {
        asignaturaDestinoId: proposal.asignaturaDestinoId,
        origenIds: groupOriginIds,
      });
      if (requestId !== detailRequestId.current) return;
      setIaAnalysis(current => current ? {
        ...current,
        estado: 'AnalizadoConIa',
        propuestas: current.propuestas.some(item => proposalReviewKey(item) === proposalReviewKey(grouped))
          ? current.propuestas
          : [...current.propuestas, grouped],
      } : current);
      setSelectedExpediente(current => current ? { ...current, estado: 'AnalizadoConIa' } : current);
      setExpedientes(current => current.map(item => item.id === expedienteId
        ? { ...item, estado: 'AnalizadoConIa' }
        : item));
      setGroupFormKey(null);
      setGroupOriginIds([]);
      setReviewFormKey(proposalReviewKey(grouped));
      setReviewMode('custom');
      setReviewOutcome('');
      setReviewComment('');
      setRecommendHomologar(false);
      setNotice('Propuesta agrupada recalculada. Registra ahora la decisión del docente.');
    } catch (cause) {
      if (requestId === detailRequestId.current) {
        setIaError(cause instanceof Error ? cause.message : 'No se pudo recalcular la propuesta agrupada.');
      }
    } finally {
      if (requestId === detailRequestId.current) setGrouping(false);
    }
  }

  async function saveProposalReview(
    proposal: PropuestaExpedienteIa,
    acceptIa: boolean,
    comment = reviewComment,
    includeRecommendation = recommendHomologar,
  ) {
    if (!selectedExpediente) return;
    const key = proposalReviewKey(proposal);
    const expedienteId = selectedExpediente.id;
    const requestId = detailRequestId.current;
    const calculated = Number(proposal.evaluacion.reglas.resultado);
    const needsJustification = !acceptIa || calculated !== 0;
    if ((!acceptIa && !reviewOutcome) || (needsJustification && !comment.trim())) {
      setIaError(acceptIa
        ? 'Justifica por qué aceptas una propuesta que no fue homologable.'
        : 'Selecciona tu conclusión y escribe brevemente el motivo.');
      return;
    }

    setSavingReviewKey(key);
    setIaError('');
    setNotice('');
    try {
      const observationParts = [comment.trim(), includeRecommendation ? 'Es recomendable homologar' : '']
        .filter(Boolean);
      const revision = await expedientesService.revisarPropuestaIa(expedienteId, {
        asignaturaDestinoId: proposal.asignaturaDestinoId,
        origenIds: proposal.origenes.map(origen => origen.asignaturaCursadaId),
        aceptarPropuestaIa: acceptIa,
        resultadoPropio: acceptIa ? null : Number(reviewOutcome),
        observacion: observationParts.join(' · ') || null,
      });
      if (requestId !== detailRequestId.current) return;
      let refreshedDecisions: DecisionExpediente[] | null = null;
      try {
        refreshedDecisions = await expedientesService.listarDecisiones(expedienteId);
      } catch {
        // The decision is saved; the progress can be refreshed by reopening the expediente.
      }
      if (requestId !== detailRequestId.current) return;
      setIaAnalysis(current => current ? {
        ...current,
        estado: 'EnRevision',
        propuestas: current.propuestas.map(item => proposalReviewKey(item) === key
          ? { ...item, revisionesDocente: [...(item.revisionesDocente ?? []), revision] }
          : item),
      } : current);
      setSelectedExpediente(current => current ? { ...current, estado: 'EnRevision' } : current);
      setExpedientes(current => current.map(item => item.id === expedienteId
        ? { ...item, estado: 'EnRevision' }
        : item));
      if (refreshedDecisions) setDecisions(refreshedDecisions);
      setReviewFormKey(null);
      setReviewMode(null);
      setReviewOutcome('');
      setReviewComment('');
      setRecommendHomologar(false);
      setNotice(acceptIa
        ? 'Se guardó la aceptación de la propuesta de IA como resultado del profesor.'
        : 'Se guardó tu conclusión y el motivo en el expediente.');
    } catch (cause) {
      if (requestId === detailRequestId.current) {
        setIaError(cause instanceof Error ? cause.message : 'No se pudo guardar la decisión del profesor.');
      }
    } finally {
      if (requestId === detailRequestId.current) setSavingReviewKey(null);
    }
  }

  async function resolveExpediente() {
    if (!selectedExpediente) return;
    const expedienteId = selectedExpediente.id;
    const requestId = detailRequestId.current;
    setResolving(true);
    setIaError('');
    setNotice('');
    try {
      const result = await expedientesService.resolver(expedienteId);
      if (requestId !== detailRequestId.current) return;
      setSelectedExpediente(current => current ? { ...current, estado: result.estado } : current);
      setExpedientes(current => current.map(item => item.id === expedienteId
        ? { ...item, estado: result.estado }
        : item));
      setNotice('El expediente se resolvió y quedó en modo de solo lectura.');
    } catch (cause) {
      if (requestId === detailRequestId.current) {
        setIaError(cause instanceof Error ? cause.message : 'No se pudo resolver el expediente.');
      }
    } finally {
      if (requestId === detailRequestId.current) setResolving(false);
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

  const soloLectura = selectedExpediente != null &&
    [2, 3, 5, 'Finalizado', 'Anulado', 'Resuelto'].includes(selectedExpediente.estado);
  const puedeEditar = canCreate && !soloLectura;

  const selectedStudent = students.find(student => student.id === selectedExpediente?.estudianteId);
  const selectedCareer = careers.find(career => career.id === selectedExpediente?.carreraDestinoId);
  const selectedMalla = detailMallas.find(malla => malla.id === selectedExpediente?.mallaDestinoId);
  const proposalsByDestination = iaAnalysis?.propuestas.reduce<Record<string, PropuestaExpedienteIa[]>>((groups, proposal) => {
    (groups[proposal.asignaturaDestinoId] ??= []).push(proposal);
    return groups;
  }, {}) ?? {};
  const analysisProposals = iaAnalysis?.propuestas ?? [];
  const destinationIds = [...new Set(analysisProposals.map(proposal => proposal.asignaturaDestinoId))];
  const decisionsByDestination = new Map(decisions.map(decision => [decision.asignaturaDestinoId, decision]));
  const decidedDestinationCount = destinationIds.filter(id => {
    const decision = decisionsByDestination.get(id);
    return decision != null && decision.resultadoFinal !== 3
      && decisionMatchesProposal(decision, id, analysisProposals);
  }).length;
  const canResolve = destinationIds.length > 0
    && decidedDestinationCount === destinationIds.length
    && decisions.every(decision => decision.resultadoFinal !== 3)
    && reviewFormKey === null
    && groupFormKey === null
    && savingReviewKey === null
    && !grouping;
  const decisionProgressPercent = destinationIds.length
    ? Math.round((decidedDestinationCount / destinationIds.length) * 100)
    : 0;

  return <main className="catalog-page expediente-page">
    <header className="catalog-header">
      <div>
        <p className="catalog-eyebrow">HOMOLOGACIONES FISEI · EXPEDIENTES ACADÉMICOS</p>
        <h1>Expedientes</h1>
        <p>Crea expedientes de homologación y consulta su estado actual.</p>
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
      <section className="eligibility-panel" aria-labelledby="eligibility-heading">
        <div className="students-panel-heading">
          <div>
            <h3 id="eligibility-heading">Elegibilidad por reglas</h3>
            <p>Selecciona las asignaturas de origen y destino para comparar créditos, contenidos y carga horaria.</p>
          </div>
          {puedeEditar && <button className="students-secondary" type="button" onClick={() => { void preloadPairs(); }} disabled={saving || loadingDetail || originCourses.length === 0 || destinationCourses.length === 0}>
            {saving ? 'Precargando…' : 'Precargar coincidencias'}
          </button>}
        </div>
        {loadingDetail ? <p className="catalog-message" role="status">Cargando historial, asignaturas y pares…</p> : <>
          {originCourses.length === 0 && <p className="catalog-message">El estudiante no tiene asignaturas cursadas disponibles en su historial.</p>}
          {destinationCourses.length === 0 && <p className="catalog-message">La malla de destino no tiene asignaturas disponibles.</p>}
          {puedeEditar && <div className="eligibility-pair-picker">
            <label>Asignatura de origen
              <select value={pairOriginId} onChange={event => setPairOriginId(event.target.value)} disabled={originCourses.length === 0}>
                <option value="">Selecciona del historial</option>
                {originCourses.filter(course => course.id).map(course => <option key={course.id} value={course.id}>
                  {course.codigo ? `${course.codigo} · ` : ''}{course.nombre} · {course.creditos ?? '—'} créditos
                </option>)}
              </select>
            </label>
            <label>Asignatura de destino
              <select value={pairDestinationId} onChange={event => setPairDestinationId(event.target.value)} disabled={destinationCourses.length === 0}>
                <option value="">Selecciona de la malla</option>
                {destinationCourses.map(course => <option key={course.id} value={course.id}>
                  {course.codigo} · {course.nombre} · {course.creditos} créditos
                </option>)}
              </select>
            </label>
            <button className="students-secondary" type="button" onClick={addPair} disabled={!pairOriginId || !pairDestinationId}>Agregar par</button>
          </div>}
          <div className="student-table-wrap eligibility-results-wrap">
            <table className="student-table eligibility-results-table">
              <thead><tr><th>Asignatura de origen</th><th>Asignatura de destino</th><th>Créditos</th><th>% contenidos</th><th>Umbral</th><th>Resultado</th><th>Carga horaria</th>{puedeEditar && <th>Acción</th>}</tr></thead>
              <tbody>{pairs.map(pair => {
                const origin = originCourses.find(course => course.id === pair.asignaturaCursadaId);
                const destination = destinationCourses.find(course => course.id === pair.asignaturaDestinoId);
                const result = results.find(item => item.parId === pair.id
                  || (item.asignaturaCursadaId === pair.asignaturaCursadaId && item.asignaturaDestinoId === pair.asignaturaDestinoId));
                const rules = result?.evaluacion.reglas;
                const hoursWarning = rules?.advertencias.find(item => /horas|carga horaria/i.test(item));
                return <tr key={pair.id}>
                  <td>{result?.nombreOrigen ?? origin?.nombre ?? 'Asignatura no disponible'}{origin?.codigo ? <small>{origin.codigo}</small> : null}</td>
                  <td>{result?.nombreDestino ?? destination?.nombre ?? 'Asignatura no disponible'}{destination?.codigo ? <small>{destination.codigo}</small> : null}</td>
                  <td>{origin?.creditos ?? '—'} / {destination?.creditos ?? '—'}{rules?.razonCreditos != null && <small>{formatPercent(rules.razonCreditos)}</small>}</td>
                  <td>{rules?.coberturaContenidos == null ? '—' : formatPercent(rules.coberturaContenidos)}</td>
                  <td>Créditos 80%<small>Contenidos 80%</small></td>
                  <td>{rules ? resultadoLabel(rules.resultado) : 'Pendiente'}</td>
                  <td>{rules?.razonHoras == null ? '—' : formatPercent(rules.razonHoras)}{hoursWarning && <small className="eligibility-warning">{hoursWarning}</small>}</td>
                  {puedeEditar && <td><button className="students-remove" type="button" onClick={() => {
                    setPairs(current => current.filter(item => item.id !== pair.id));
                    setResults(current => current.filter(item => item.parId !== pair.id));
                  }}>Quitar</button></td>}
                </tr>;
              })}
              {pairs.length === 0 && <tr><td colSpan={puedeEditar ? 8 : 7} className="eligibility-empty">Aún no hay pares. Selecciona asignaturas o precarga coincidencias por nombre.</td></tr>}</tbody>
            </table>
          </div>
          {puedeEditar && <div className="eligibility-actions">
            <button className="students-primary" type="button" onClick={() => { void evaluatePairs(); }} disabled={evaluating || loadingDetail || pairs.length === 0}>
              {evaluating ? 'Evaluando…' : 'Evaluar por reglas'}
            </button>
            {results.length > 0 && <span>Evaluación guardada · {new Date().toLocaleString()}</span>}
          </div>}
          {results.length > 0 && <p className="eligibility-threshold-note">Umbrales aplicados: mínimo 80% de créditos y 80% de contenidos. La carga horaria se muestra como advertencia cuando queda bajo el 80%.</p>}
        </>}
      </section>
      <section className="ia-analysis-panel" aria-labelledby="ia-analysis-heading">
        <div className="students-panel-heading ia-analysis-header">
          <div>
            <h3 id="ia-analysis-heading">Análisis con IA</h3>
            <p>Genera propuestas de equivalencia agrupadas por asignatura de destino. La decisión final corresponde al profesor.</p>
          </div>
          {puedeEditar && <button className="students-primary" type="button" onClick={() => { void analyzeWithIa(); }} disabled={analyzingIa || loadingDetail}>
            {analyzingIa ? 'Analizando expediente…' : 'Analizar con IA'}
          </button>}
        </div>
        {iaError && <p className="ia-error" role="alert">{iaError}</p>}
        {analyzingIa && <p className="catalog-message" role="status">El agente está analizando los contenidos del expediente. Esto puede tardar unos minutos.</p>}
        {iaAnalysis && <>
          <p className="ia-analysis-meta" role="status">Análisis {iaAnalysis.estado.toLowerCase()} · {new Date(iaAnalysis.ejecutadoEn).toLocaleString()} · {iaAnalysis.propuestas.length} propuestas</p>
          <div className="ia-decision-progress" aria-live="polite">
            <div>
              <strong>Avance de decisiones</strong>
              <span>{decidedDestinationCount} de {destinationIds.length} asignaturas de destino decididas</span>
            </div>
            <div className="ia-progress-track" role="progressbar" aria-label="Avance de decisiones del expediente"
              aria-valuemin={0} aria-valuemax={100} aria-valuenow={decisionProgressPercent}>
              <span style={{ width: `${decisionProgressPercent}%` }} />
            </div>
            <ul className="ia-progress-list">{destinationIds.map(id => {
              const decision = decisionsByDestination.get(id);
              const proposal = analysisProposals.find(item => item.asignaturaDestinoId === id);
              const complete = decision != null && decision.resultadoFinal !== 3
                && decisionMatchesProposal(decision, id, analysisProposals);
              return <li key={id} className={complete ? 'decision-complete' : 'decision-pending'}>
                <span>{proposal?.nombreDestino ?? id}</span>
                <strong>{complete && decision
                  ? `${resultadoLabel(decision.resultadoCalculado)} → ${resultadoLabel(decision.resultadoFinal)}`
                  : 'Pendiente'}</strong>
              </li>;
            })}</ul>
            {puedeEditar && <button className="students-primary" type="button" onClick={() => { void resolveExpediente(); }} disabled={!canResolve || resolving}>
              {resolving ? 'Resolviendo…' : 'Resolver expediente'}
            </button>}
            {!canResolve && puedeEditar && <small>Registra una decisión concluyente para cada asignatura de destino antes de resolver.</small>}
            {soloLectura && <small>Expediente resuelto: consulta disponible en solo lectura.</small>}
          </div>
          {iaAnalysis.advertencias.length > 0 && <div className="ia-warnings" role="note">
            <strong>Advertencias del análisis</strong>
            <ul>{iaAnalysis.advertencias.map((warning, index) => <li key={`${index}-${warning}`}>{warning}</li>)}</ul>
          </div>}
          {Object.keys(proposalsByDestination).length === 0
            ? <p className="catalog-message">El análisis terminó sin propuestas para las asignaturas de destino.</p>
            : <div className="ia-destinations">{Object.entries(proposalsByDestination).map(([destinationId, proposals]) => {
              const destination = destinationCourses.find(course => course.id === destinationId);
              return <section className="ia-destination" key={destinationId} aria-label={`Propuestas para ${proposals[0].nombreDestino}`}>
                <h4>{destination?.codigo ? `${destination.codigo} · ` : ''}{proposals[0].nombreDestino}</h4>
                <div className="ia-proposals">{proposals.map((proposal, index) => {
                  const rules = proposal.evaluacion.reglas;
                  const coverage = proposal.evaluacion.cobertura;
                  const contentPercent = coverage.porcentaje ?? rules.coberturaContenidos;
                  const originNames = proposal.origenes.map(origin => origin.nombre);
                  const reviewKey = proposalReviewKey(proposal);
                  const latestReview = proposal.revisionesDocente?.[proposal.revisionesDocente.length - 1];
                  const currentDecision = decisionsByDestination.get(destinationId);
                  const hasCurrentDecision = currentDecision != null
                    && decisionMatchesProposal(currentDecision, destinationId, analysisProposals)
                    && decisionKey(currentDecision) === reviewKey;
                  const serviceExplanation = proposal.evaluacion.explicacion?.trim();
                  const explanation = serviceExplanation || explicarPropuesta(proposal);
                  const explanationSource = serviceExplanation
                    ? proposal.evaluacion.fuenteExplicacion || 'plantilla'
                    : 'plantilla';
                  return <article className="ia-proposal-card" key={`${destinationId}-${index}`}>
                    <div className="ia-proposal-title">
                      <div><h5>{originNames.length > 1 ? 'Propuesta combinada' : 'Asignatura de origen'}</h5>
                        <p>{originNames.length ? originNames.join(' + ') : 'Origen no disponible'}</p></div>
                      <div className="ia-result-stack">
                        <span className={`ia-result ${resultadoClass(rules.resultado)}`}>IA: {resultadoLabel(rules.resultado)}</span>
                        {currentDecision && hasCurrentDecision && <span className={`ia-result ia-result-teacher ${resultadoClass(currentDecision.resultadoFinal)}`}>
                          Decisión vigente: {resultadoLabel(currentDecision.resultadoFinal)}
                        </span>}
                        {latestReview && !hasCurrentDecision && <span className={`ia-result ia-result-teacher ${resultadoClass(latestReview.resultadoFinal)}`}>
                          Última decisión registrada: {resultadoLabel(latestReview.resultadoFinal)}
                        </span>}
                      </div>
                    </div>
                    <dl className="ia-proposal-metrics">
                      <div><dt>Créditos</dt><dd>{rules.razonCreditos == null ? 'Datos incompletos' : formatPercent(rules.razonCreditos)}</dd></div>
                      <div><dt>Contenidos</dt><dd>{contentPercent == null ? 'Datos incompletos' : formatPercent(contentPercent)}</dd></div>
                      <div><dt>Carga horaria</dt><dd>{rules.razonHoras == null ? '—' : formatPercent(rules.razonHoras)}</dd></div>
                    </dl>
                    <div className="ia-proposal-explanation">
                      <div className="ia-explanation-heading">
                        <strong>Explicación de la propuesta</strong>
                        <span className={`ia-explanation-source ${explicacionEsLlm(explanationSource) ? 'source-llm' : 'source-template'}`}>
                          Origen: {explicacionEsLlm(explanationSource) ? 'LLM' : 'Plantilla'}
                        </span>
                      </div>
                      <p>{explanation}</p>
                    </div>
                    <div className="ia-topic-columns">
                      <div><h6>Temas coincidentes ({coverage.temasCoincidentes.length})</h6>
                        {coverage.temasCoincidentes.length ? <ul className="ia-topic-list">{coverage.temasCoincidentes.map((topic, topicIndex) => <li key={`${topic.codigoDestino}-${topicIndex}`}>
                          <span>{topic.descripcion}</span><small>{topic.similitud == null ? 'Coincidencia sugerida' : `Similitud ${formatPercent(topic.similitud)}`}{topic.codigoOrigen ? ` · ${topic.codigoOrigen}` : ''}</small>
                          {topic.evidencia && <small className="ia-topic-evidence">Evidencia: {topic.evidencia}</small>}
                        </li>)}</ul> : <p className="ia-topic-empty">No se identificaron temas coincidentes.</p>}
                      </div>
                      <div><h6>Temas ausentes ({coverage.temasAusentes.length})</h6>
                        {coverage.temasAusentes.length ? <ul className="ia-topic-list">{coverage.temasAusentes.map((topic, topicIndex) => <li key={`${topic.codigoDestino}-${topicIndex}`}>
                          <span>{topic.descripcion}</span>{topic.horas != null && <small>{topic.horas} horas de destino</small>}
                        </li>)}</ul> : <p className="ia-topic-empty">No se identificaron temas ausentes.</p>}
                      </div>
                    </div>
                    {rules.advertencias.length > 0 && <ul className="ia-rule-warnings">{rules.advertencias.map((warning, warningIndex) => <li key={`${warningIndex}-${warning}`}>{warning}</li>)}</ul>}
                    {proposal.revisionesDocente?.length ? <details className="ia-review-history">
                      <summary>{proposal.revisionesDocente.length} decisión(es) previa(s) para este origen y destino</summary>
                      <ol>{proposal.revisionesDocente.map((review, reviewIndex) => <li key={`${review.revisadoEn}-${reviewIndex}`}>
                        <strong>{review.tipo === 'AceptaPropuestaIa' ? 'Aceptó la propuesta de IA' : 'Registró una conclusión propia'}</strong>
                        <span>Calculado: {resultadoLabel(rules.resultado)} → decisión: {resultadoLabel(review.resultadoFinal)}</span>
                        {review.observacion && <span>Justificación: {review.observacion}</span>}
                        <small>{review.profesorNombre || 'Profesor'} · {new Date(review.revisadoEn).toLocaleString()}</small>
                      </li>)}</ol>
                    </details> : null}
                    {proposal.precedentes?.length ? <details className="ia-review-history">
                      <summary>{proposal.precedentes.length} precedente(s) del histórico para este origen y destino</summary>
                      <ol>{proposal.precedentes.map((precedente, precedenteIndex) => <li key={`${precedente.fecha}-${precedenteIndex}`}>
                        <strong>Decisión anterior: {resultadoLabel(precedente.resultadoFinal)}</strong>
                        <span>Resultado calculado entonces: {resultadoLabel(precedente.resultadoCalculado)}</span>
                        {precedente.justificacion && <span>Justificación: {precedente.justificacion}</span>}
                        <small>{new Date(precedente.fecha).toLocaleString()}</small>
                      </li>)}</ol>
                    </details> : null}
                    {puedeEditar && <div className="ia-review-controls">
                      <div className="ia-review-actions">
                        <button className="students-primary" type="button" onClick={() => acceptIaProposal(proposal)} disabled={savingReviewKey !== null || grouping}>
                          {savingReviewKey === reviewKey ? 'Guardando…' : 'Aceptar resultado de IA'}
                        </button>
                        <button className="students-secondary" type="button" onClick={() => openOwnConclusion(proposal)} disabled={savingReviewKey !== null}>
                          Modificar o rechazar
                        </button>
                        <button className="students-secondary" type="button" onClick={() => {
                          if (groupFormKey === reviewKey) {
                            setGroupFormKey(null);
                          } else {
                            setReviewFormKey(null);
                            setReviewMode(null);
                            setGroupOriginIds(proposal.origenes.map(origin => origin.asignaturaCursadaId));
                            setGroupFormKey(reviewKey);
                          }
                        }} disabled={savingReviewKey !== null || grouping}>
                          Modificar / agrupar origen
                        </button>
                      </div>
                      {groupFormKey === reviewKey && <form className="ia-review-form ia-group-form" onSubmit={event => { event.preventDefault(); void createGroupedProposal(proposal); }}>
                        <strong>Recalcular con asignaturas de origen</strong>
                        <p>Selecciona dos o más asignaturas. Para cambiar a un solo origen, elige una propuesta individual de esta asignatura destino.</p>
                        {originCourses.filter((course): course is AsignaturaCursada & { id: string } => Boolean(course.id)).map(course => <label className="ia-origin-option" key={course.id}>
                          <input type="checkbox" checked={groupOriginIds.includes(course.id)} onChange={() => toggleGroupOrigin(course.id)} />
                          <span>{course.codigo ? `${course.codigo} · ` : ''}{course.nombre}</span>
                        </label>)}
                        <div className="ia-review-actions">
                          <button className="students-primary" type="submit" disabled={groupOriginIds.length < 2 || grouping}>
                            {grouping ? 'Recalculando…' : 'Recalcular propuesta agrupada'}
                          </button>
                          <button className="students-secondary" type="button" onClick={() => setGroupFormKey(null)} disabled={grouping}>Cancelar</button>
                        </div>
                      </form>}
                      {reviewFormKey === reviewKey && reviewMode && <form className="ia-review-form" onSubmit={event => {
                        event.preventDefault();
                        void saveProposalReview(proposal, reviewMode === 'accept');
                      }}>
                        {reviewMode === 'custom' && <label>Resultado de mi decisión
                          <select value={reviewOutcome} onChange={event => setReviewOutcome(event.target.value)} required>
                            <option value="">Selecciona el resultado</option>
                            <option value="0">Homologable</option>
                            <option value="1">Rechazo por créditos</option>
                            <option value="2">Rechazo por contenidos</option>
                            <option value="3">Datos incompletos</option>
                            <option value="4">Transversal directa</option>
                            <option value="5">Origen no aprobado</option>
                          </select>
                        </label>}
                        <label>Justificación de la decisión
                          <textarea value={reviewComment} onChange={event => setReviewComment(event.target.value)} maxLength={2000} rows={3}
                            required={reviewMode === 'custom' || Number(rules.resultado) !== 0}
                            placeholder={reviewMode === 'accept' ? 'Justifica por qué aceptas esta propuesta no homologable.' : 'Explica brevemente por qué cambias o rechazas el resultado.'} />
                        </label>
                        {Number(rules.resultado) === 1 && <label className="ia-annotation-option">
                          <input type="checkbox" checked={recommendHomologar} onChange={event => setRecommendHomologar(event.target.checked)} />
                          <span>Es recomendable homologar (anotación; no cambia el resultado calculado)</span>
                        </label>}
                        <div className="ia-review-actions">
                          <button className="students-primary" type="submit" disabled={savingReviewKey !== null || (reviewMode === 'custom' && !reviewOutcome) || ((reviewMode === 'custom' || Number(rules.resultado) !== 0) && !reviewComment.trim())}>
                            {savingReviewKey === reviewKey ? 'Guardando…' : reviewMode === 'accept' ? 'Aceptar propuesta con justificación' : 'Guardar mi decisión'}
                          </button>
                          <button className="students-secondary" type="button" onClick={() => { setReviewFormKey(null); setReviewMode(null); }} disabled={savingReviewKey !== null}>Cancelar</button>
                        </div>
                      </form>}
                    </div>}
                  </article>;
                })}</div>
              </section>;
            })}</div>}
        </>}
      </section>
    </section>}
  </main>;
}

function formatPercent(value: number): string {
  return new Intl.NumberFormat('es-EC', { style: 'percent', maximumFractionDigits: 2 }).format(value);
}

function proposalReviewKey(proposal: PropuestaExpedienteIa): string {
  const origins = proposal.origenes.map(origin => origin.asignaturaCursadaId).sort().join(',');
  return `${proposal.asignaturaDestinoId}:${origins}`;
}

function decisionKey(decision: DecisionExpediente): string {
  try {
    const origins = (JSON.parse(decision.origenIdsJson) as string[]).sort().join(',');
    return `${decision.asignaturaDestinoId}:${origins}`;
  } catch {
    return '';
  }
}

function decisionMatchesProposal(
  decision: DecisionExpediente,
  destinationId: string,
  proposals: PropuestaExpedienteIa[],
): boolean {
  const key = decisionKey(decision);
  return Boolean(key) && proposals.some(proposal => proposal.asignaturaDestinoId === destinationId
    && proposalReviewKey(proposal) === key);
}

function explicarPropuesta(proposal: PropuestaExpedienteIa): string {
  const { reglas, cobertura } = proposal.evaluacion;
  const total = cobertura.temasCoincidentes.length + cobertura.temasAusentes.length;
  const porcentaje = cobertura.porcentaje ?? reglas.coberturaContenidos;
  const partes = [`La recomendación es “${resultadoLabel(reglas.resultado)}”.`];

  if (porcentaje == null || total === 0) {
    partes.push('No hay información suficiente de contenidos para calcular la cobertura.');
  } else {
    partes.push(`Coinciden ${cobertura.temasCoincidentes.length} de ${total} temas destino (${formatPercent(porcentaje)}).`);
  }
  partes.push(reglas.razonCreditos == null
    ? 'No se pudo calcular la razón de créditos.'
    : `La razón de créditos es ${formatPercent(reglas.razonCreditos)}.`);
  if (reglas.razonHoras != null) partes.push(`La razón de horas es ${formatPercent(reglas.razonHoras)}.`);
  if (cobertura.temasAusentes.length > 0) partes.push(`Quedan ${cobertura.temasAusentes.length} temas destino sin coincidencia suficiente.`);
  partes.push('La evidencia por tema aparece abajo; el profesor conserva la decisión final.');
  return partes.join(' ');
}

function explicacionEsLlm(source: string): boolean {
  const normalized = source.trim().toLocaleLowerCase();
  return normalized === 'llm' || normalized.includes('ollama') || normalized.includes('gemini');
}

function resultadoLabel(value: string | number): string {
  const labels: Record<number, string> = {
    0: 'Homologable',
    1: 'Rechazo por créditos',
    2: 'Rechazo por contenidos',
    3: 'Datos incompletos',
    4: 'Transversal directa',
    5: 'Origen no aprobado',
  };
  if (typeof value === 'number') return labels[value] ?? `Resultado ${value}`;
  const labelsByName: Record<string, string> = {
    Homologable: 'Homologable',
    RechazoPorCreditos: 'Rechazo por créditos',
    RechazoPorContenidos: 'Rechazo por contenidos',
    DatosIncompletos: 'Datos incompletos',
    TransversalDirecta: 'Transversal directa',
    OrigenNoAprobado: 'Origen no aprobado',
  };
  return labelsByName[value] ?? value;
}

function resultadoClass(value: string | number): string {
  const classesByNumber: Record<number, string> = {
    0: 'ia-result-homologable',
    1: 'ia-result-rechazoporcreditos',
    2: 'ia-result-rechazoporcontenidos',
    3: 'ia-result-datosincompletos',
    4: 'ia-result-transversaldirecta',
    5: 'ia-result-origennoaprobado',
  };
  const numericValue = typeof value === 'number' ? value : Number(value);
  if (Number.isInteger(numericValue) && String(value).trim() !== '') {
    return classesByNumber[numericValue] ?? 'ia-result-unknown';
  }
  const normalized = String(value).normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .toLowerCase().replace(/[^a-z0-9]+/g, '');
  return `ia-result-${normalized || 'unknown'}`;
}

function estadoLabel(estado: number | string): string {
  if (typeof estado === 'number') {
    return estadosNumericos[estado] ?? `Estado no reconocido (${estado})`;
  }
  if (estado === 'Resuelto') return 'Resuelto';
  if (estado === 'AnalizadoConIa') return 'Analizado con IA';
  if (estado === 'EnRevision') return 'En revisión';
  return estadosExpediente.find(value => value === estado) ?? `Estado no reconocido (${estado})`;
}

const estadosNumericos: Record<number, (typeof estadosExpediente)[number]> = {
  0: 'Borrador',
  1: 'En revisión',
  2: 'Finalizado',
  3: 'Anulado',
  4: 'Analizado con IA',
  5: 'Resuelto',
};

function estadoClass(estado: number | string): string {
  return estadoLabel(estado).toLocaleLowerCase().replaceAll(' ', '-').replaceAll('ó', 'o');
}
