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
  type SeleccionPropuesta,
} from '../expedientesService';
import { studentsService, type AsignaturaCursada, type Estudiante } from '../studentsService';

interface ExpedientesPageProps {
  canCreate: boolean;
}

export default function ExpedientesPage({ canCreate }: ExpedientesPageProps) {
  const [detailStudent, setDetailStudent] = useState<Estudiante | null>(null);
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
  const [selection, setSelection] = useState<SeleccionPropuesta[]>([]);
  const [selecting, setSelecting] = useState(false);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const [showDiagnostics, setShowDiagnostics] = useState(false);
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
  const detailPanelRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (expandedKey) { detailPanelRef.current?.focus({ preventScroll: true }); detailPanelRef.current?.scrollIntoView?.({ behavior: 'smooth', block: 'start' }); }
  }, [expandedKey]);

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
    setDetailStudent(null);
    setSelection([]); setExpandedKey(null); setSelecting(false);
    setShowDiagnostics(false);
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
      const student = students.find(item => item.id === expediente.estudianteId) ?? await studentsService.obtener(expediente.estudianteId);
      const editable = canCreate && ![2, 3, 5, 'Finalizado', 'Anulado', 'Resuelto'].includes(expediente.estado);
      const [rows, pairRows, analisisGuardado, decisionRows, summary] = await Promise.all([
        catalogService.obtenerMallas(expediente.carreraDestinoId),
        editable && student.historial?.asignaturas?.length ? expedientesService.precargarPares(expediente.id) : expedientesService.listarPares(expediente.id),
        expedientesService.consultarAnalisis(expediente.id),
        expedientesService.listarDecisiones(expediente.id),
        expedientesService.consultarSeleccion(expediente.id),
      ]);
      const levels = await catalogService.obtenerNiveles(expediente.mallaDestinoId);
      const assignmentGroups = await Promise.all(levels.map(level =>
        catalogService.obtenerAsignaturas(expediente.mallaDestinoId, level.id, ''),
      ));
      if (requestId === detailRequestId.current) {
        setDetailStudent(student);
        setDetailMallas(rows);
        setOriginCourses(student?.historial?.asignaturas ?? []);
        setDestinationCourses(assignmentGroups.flat());
        setPairs(pairRows);
        setIaAnalysis(analisisGuardado);
        setDecisions(decisionRows);
        setSelection(summary.seleccion); setResults(summary.resultados);
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
    setPairOriginId('');
    setPairDestinationId('');
    setError('');
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

  async function analyzeWithIa(recalcular = false) {
    if (!selectedExpediente) return;
    const expedienteId = selectedExpediente.id;
    const requestId = detailRequestId.current;
    setAnalyzingIa(true);
    setIaError('');
    setNotice('');
    try {
      const analysis = await expedientesService.analizarConIa(expedienteId, recalcular);
      if (requestId !== detailRequestId.current) return;
      setIaAnalysis(analysis);
      if (recalcular) setSelection([]);
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
    if ([0, 4].includes(calculated)) {
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
    const needsJustification = !acceptIa || ![0, 4].includes(calculated);
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
      const finalResult = acceptIa ? calculated : Number(reviewOutcome);
      setSelection(current => [...current.filter(item => item.asignaturaDestinoId !== proposal.asignaturaDestinoId),
        ...([0, 4].includes(finalResult) ? [{ asignaturaDestinoId: proposal.asignaturaDestinoId, origenIds: proposal.origenes.map(o => o.asignaturaCursadaId) }] : [])]);
      try {
        const [summary, analysis] = await Promise.all([expedientesService.consultarSeleccion(expedienteId), expedientesService.consultarAnalisis(expedienteId)]);
        if (requestId !== detailRequestId.current) return;
        setSelection(summary.seleccion); setIaAnalysis(analysis);
      } catch { /* La decisión ya se guardó; se conserva el estado local y se puede reabrir el expediente. */ }
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
      if (students.find(student => student.id === studentId)?.historial?.carreraOrigenId === careerId)
        throw new Error('La carrera de destino debe ser diferente a la carrera de origen.');
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

  const selectedStudent = detailStudent ?? students.find(student => student.id === selectedExpediente?.estudianteId);
  const selectedCareer = careers.find(career => career.id === selectedExpediente?.carreraDestinoId);
  const originCareer = careers.find(career => career.id === selectedStudent?.historial?.carreraOrigenId);
  const originLabel = originCareer?.codigo ?? selectedStudent?.historial?.carreraOrigenNombre ?? 'Origen sin registrar';
  const destinationLabel = selectedCareer?.codigo ?? 'Destino sin registrar';
  const createStudent = students.find(student => student.id === studentId);
  const availableCareers = careers.filter(career => career.id !== createStudent?.historial?.carreraOrigenId);
  const selectedMalla = detailMallas.find(malla => malla.id === selectedExpediente?.mallaDestinoId);
  const viableProposals = iaAnalysis?.propuestas.filter(proposal => esRecomendable(proposal)) ?? [];
  const nearProposals = iaAnalysis?.propuestas.filter(esCercana) ?? [];
  const visibleIa = (iaAnalysis?.propuestas ?? []).filter(p => showDiagnostics || esRecomendable(p) || esCercana(p));
  const allReviewProposals = [...(iaAnalysis?.propuestas ?? [])];
  for (const result of results) {
    const proposal = propuestaDePar(result);
    if (!allReviewProposals.some(p => proposalReviewKey(p) === proposalReviewKey(proposal))) allReviewProposals.push(proposal);
  }
  const analysisProposals = allReviewProposals;
  const selectedKeys = new Set(selection.map(item => `${item.asignaturaDestinoId}:${[...item.origenIds].sort().join(',')}`));
  const definitive = allReviewProposals.filter(p => selectedKeys.has(proposalReviewKey(p)));
  const displayAnalysis = iaAnalysis ?? { id: '', estado: String(selectedExpediente?.estado ?? ''), ejecutadoEn: '', propuestas: [], advertencias: [] };
  const proposalsByDestination = allReviewProposals.filter(p => proposalReviewKey(p) === expandedKey).reduce<Record<string, PropuestaExpedienteIa[]>>((groups, proposal) => {
    (groups[proposal.asignaturaDestinoId] ??= []).push(proposal); return groups;
  }, {});
  async function toggleSelection(proposal: PropuestaExpedienteIa) {
    if (!selectedExpediente || selecting) return;
    const key = proposalReviewKey(proposal);
    const next = selectedKeys.has(key)
      ? selection.filter(item => `${item.asignaturaDestinoId}:${[...item.origenIds].sort().join(',')}` !== key)
      : [...selection, { asignaturaDestinoId: proposal.asignaturaDestinoId, origenIds: proposal.origenes.map(o => o.asignaturaCursadaId) }];
    if (!selectedKeys.has(key) && selection.some(item => item.asignaturaDestinoId === proposal.asignaturaDestinoId
      || item.origenIds.some(id => proposal.origenes.some(o => o.asignaturaCursadaId === id)))) {
      setIaError('Retira primero la selección incompatible: solo se permite una propuesta por destino y cada origen puede respaldar un destino.'); return;
    }
    const requestId = detailRequestId.current;
    setSelecting(true); setIaError('');
    try { const saved = await expedientesService.guardarSeleccion(selectedExpediente.id, next);
      if (requestId === detailRequestId.current) setSelection(saved);
    } catch (cause) { if (requestId === detailRequestId.current) setIaError(cause instanceof Error ? cause.message : 'No se pudo guardar la selección.'); }
    finally { if (requestId === detailRequestId.current) setSelecting(false); }
  }
  function selectionCheckbox(proposal: PropuestaExpedienteIa, label: string) {
    return <input type="checkbox" aria-label={label} checked={selectedKeys.has(proposalReviewKey(proposal))}
      disabled={!puedeEditar || selecting || proposal.origenes.length === 0} onChange={() => { void toggleSelection(proposal); }} />;
  }
  function showDetail(proposal: PropuestaExpedienteIa) { setExpandedKey(proposalReviewKey(proposal)); setReviewFormKey(null); }
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
          onChange={value => { setStudentId(value); changeCareer(''); }}
        />
        <SearchableSelect
          id="expediente-career"
          key={`expediente-career-${studentId}`}
          label="Carrera de destino"
          value={careerId}
          placeholder="Busca una carrera"
          options={availableCareers.map(career => ({ value: career.id, label: `${career.nombre} · ${career.codigo}` }))}
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
      <div className="expediente-transfer" aria-label="Cambio de carrera">
        <div><small>Carrera de origen</small><strong>{originLabel}</strong><span>{originCareer?.nombre ?? selectedStudent?.historial?.carreraOrigenNombre ?? 'Sin registrar'}</span></div>
        <span className="transfer-arrow" aria-hidden="true">→</span>
        <div><small>Carrera de destino</small><strong>{destinationLabel}</strong><span>{selectedCareer?.nombre}</span></div>
      </div>
      <section className="eligibility-panel" aria-labelledby="eligibility-heading">
        <div className="students-panel-heading">
          <div>
            <h3 id="eligibility-heading">Elegibilidad por reglas</h3>
            <p>Las coincidencias se cargan al abrir el expediente. Evalúa los pares para activar su selección; la coincidencia de nombre por sí sola no confirma una homologación.</p>
          </div>

        </div>
        {loadingDetail ? <p className="catalog-message" role="status">Cargando historial, asignaturas y pares…</p> : <>
          {originCourses.length === 0 && <p className="catalog-message">El estudiante no tiene asignaturas cursadas disponibles en su historial.</p>}
          {destinationCourses.length === 0 && <p className="catalog-message">La malla de destino no tiene asignaturas disponibles.</p>}
          {puedeEditar && <div className="eligibility-pair-picker">
            <label>Asignatura de origen
              <select value={pairOriginId} onChange={event => setPairOriginId(event.target.value)} disabled={originCourses.length === 0}>
                <option value="">Selecciona del historial</option>
                {originCourses.filter(course => course.id).map(course => <option key={course.id} value={course.id}>
                  {course.codigo ? `${course.codigo} · ` : ''}{course.nombre} - {originLabel} · {course.creditos ?? '—'} créditos
                </option>)}
              </select>
            </label>
            <label>Asignatura de destino
              <select value={pairDestinationId} onChange={event => setPairDestinationId(event.target.value)} disabled={destinationCourses.length === 0}>
                <option value="">Selecciona de la malla</option>
                {destinationCourses.map(course => <option key={course.id} value={course.id}>
                  {course.codigo} · {course.nombre} - {destinationLabel} · {course.creditos} créditos
                </option>)}
              </select>
            </label>
            <button className="students-secondary" type="button" onClick={addPair} disabled={!pairOriginId || !pairDestinationId}>Agregar par</button>
          </div>}
          <div className="student-table-wrap eligibility-results-wrap">
            <table className="student-table eligibility-results-table">
              <thead><tr><th>Elegir</th><th>Asignatura de origen</th><th>Asignatura de destino</th><th>Créditos</th><th>% contenidos</th><th>Resultado</th><th>Acción</th></tr></thead>
              <tbody>{pairs.map(pair => {
                const origin = originCourses.find(course => course.id === pair.asignaturaCursadaId);
                const destination = destinationCourses.find(course => course.id === pair.asignaturaDestinoId);
                const result = results.find(item => item.parId === pair.id
                  || (item.asignaturaCursadaId === pair.asignaturaCursadaId && item.asignaturaDestinoId === pair.asignaturaDestinoId));
                const rules = result?.evaluacion.reglas;
                const proposal = result ? propuestaDePar(result) : null;
                return <tr key={pair.id}>
                  <td>{proposal ? selectionCheckbox(proposal, `Seleccionar coincidencia ${proposal.nombreDestino}`) : <input type="checkbox" disabled aria-label="Evalúa el par antes de seleccionarlo" />}</td>
                  <td>{result?.nombreOrigen ?? origin?.nombre ?? 'Asignatura no disponible'} - {originLabel}{origin?.codigo ? <small>{origin.codigo}</small> : null}</td>
                  <td>{result?.nombreDestino ?? destination?.nombre ?? 'Asignatura no disponible'} - {destinationLabel}{destination?.codigo ? <small>{destination.codigo}</small> : null}</td>
                  <td>{origin?.creditos ?? '—'} / {destination?.creditos ?? '—'}{rules?.razonCreditos != null && <small>{formatPercent(rules.razonCreditos)}</small>}</td>
                  <td>{rules?.coberturaContenidos == null ? '—' : formatPercent(rules.coberturaContenidos)}</td>

                  <td>{rules ? resultadoLabel(rules.resultado) : 'Pendiente'}</td>

                  <td>{proposal && <button type="button" className="students-secondary" onClick={() => showDetail(proposal)}>Ver detalle</button>}{puedeEditar && <button className="students-remove" type="button" onClick={() => {
                    if (selection.some(item => item.asignaturaDestinoId === pair.asignaturaDestinoId && item.origenIds.includes(pair.asignaturaCursadaId))) {
                      setIaError('Retira primero este par de la tabla definitiva.'); return;
                    }
                    setPairs(current => current.filter(item => item.id !== pair.id));
                    // La evaluación guardada conserva su evidencia hasta volver a evaluar.
                  }}>Quitar</button>}</td>
                </tr>;
              })}
              {pairs.length === 0 && <tr><td colSpan={7} className="eligibility-empty">No hay coincidencias registradas. Puedes agregar un par de origen y destino para evaluarlo.</td></tr>}</tbody>
            </table>
          </div>
          {puedeEditar && <div className="eligibility-actions">
            <button className="students-primary" type="button" onClick={() => { void evaluatePairs(); }} disabled={evaluating || loadingDetail || pairs.length === 0}>
              {evaluating ? 'Evaluando…' : 'Evaluar por reglas'}
            </button>
            {results.length > 0 && <span>Evaluación guardada · {new Date().toLocaleString()}</span>}
          </div>}
          {<p className="eligibility-threshold-note">Umbrales aplicados: mínimo 80% de créditos y 80% de contenidos. La carga horaria se muestra como advertencia cuando queda bajo el 80%.</p>}
        </>}
      </section>
      <section className="students-panel definitive-panel" aria-labelledby="definitive-heading">
        <h3 id="definitive-heading">Tabla definitiva de homologación</h3>
        <p>La selección se guarda automáticamente. Marcar una propuesta no equivale a aprobarla: registra la decisión docente desde su detalle.</p>
        <div className="student-table-wrap"><table className="student-table compact-homologation-table">
          <thead><tr><th>Elegir</th><th>Origen</th><th>Destino</th><th>Calculado</th><th>Decisión docente</th><th>Acción</th></tr></thead>
          <tbody>{definitive.map(proposal => {
            const decision = decisions.find(d => decisionKey(d) === proposalReviewKey(proposal));
            return <tr key={proposalReviewKey(proposal)}><td>{selectionCheckbox(proposal, `Retirar selección ${proposal.nombreDestino}`)}</td>
              <td>{proposal.origenes.map(o => `${o.nombre} - ${originLabel}`).join(' + ')}</td><td>{proposal.nombreDestino} - {destinationLabel}</td>
              <td>{resultadoLabel(proposal.evaluacion.reglas.resultado)}</td><td>{decision ? resultadoLabel(decision.resultadoFinal) : 'Pendiente de confirmar'}</td>
              <td><button type="button" className="students-secondary" onClick={() => showDetail(proposal)}>Ver detalle / decidir</button></td></tr>;
          })}{definitive.length === 0 && <tr><td colSpan={6}>Marca propuestas evaluadas en cualquiera de las tablas para construir tu selección.</td></tr>}</tbody>
        </table></div>
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
        {puedeEditar && iaAnalysis && decisions.length === 0 && <button type="button" className="students-secondary" disabled={analyzingIa} onClick={() => { void analyzeWithIa(true); }}>Recalcular con los datos actuales</button>}
        {iaError && <p className="ia-error" role="alert">{iaError}</p>}
        {analyzingIa && <p className="catalog-message" role="status">El agente está analizando los contenidos del expediente. Esto puede tardar unos minutos.</p>}
        {iaAnalysis && <div className="student-table-wrap"><table className="student-table compact-homologation-table" aria-label="Propuestas de IA">
          <thead><tr><th>Elegir</th><th>Origen</th><th>Destino</th><th>Créditos / contenidos</th><th>Recomendación</th><th>Acción</th></tr></thead>
          <tbody>{visibleIa.map(proposal => <tr key={proposalReviewKey(proposal)}>
            <td>{selectionCheckbox(proposal, `Seleccionar propuesta IA ${proposal.nombreDestino}`)}</td>
            <td>{proposal.origenes.map(o => `${o.nombre} - ${originLabel}`).join(' + ') || 'Sin candidato'}</td><td>{proposal.nombreDestino} - {destinationLabel}</td>
            <td>{proposal.evaluacion.reglas.razonCreditos == null ? '—' : formatPercent(proposal.evaluacion.reglas.razonCreditos)} / {proposal.evaluacion.reglas.coberturaContenidos == null ? '—' : formatPercent(proposal.evaluacion.reglas.coberturaContenidos)}</td>
            <td>{esCercana(proposal) ? 'Cercana al umbral: revisión docente' : resultadoLabel(proposal.evaluacion.reglas.resultado)}</td>
            <td><button type="button" className="students-secondary" onClick={() => showDetail(proposal)}>Ver detalle</button></td></tr>)}</tbody>
        </table></div>}
        {(iaAnalysis || results.length > 0) && <>
          <div className="ia-summary" role="status"><strong>{viableProposals.length} homologables y {nearProposals.length} casos cercanos</strong><p>Los casos cercanos tienen al menos 75 % en créditos y contenidos, con horas disponibles. Los porcentajes expresan cumplimiento, no probabilidad de aprobación; la decisión final es del docente.</p></div>
          {iaAnalysis && <label className="ia-diagnostics-toggle"><input type="checkbox" checked={showDiagnostics} onChange={event => setShowDiagnostics(event.target.checked)} />Mostrar destinos sin recomendación y diagnósticos para revisión ({displayAnalysis.propuestas.filter(proposal => !esRecomendable(proposal) && !esCercana(proposal)).length})</label>}
          {iaAnalysis && <p className="ia-analysis-meta" role="status">Análisis {displayAnalysis.estado.toLowerCase()} · {new Date(displayAnalysis.ejecutadoEn).toLocaleString()} · {displayAnalysis.propuestas.length} propuestas</p>}
          <div className="ia-decision-progress" aria-live="polite">
            <div>
              <strong>Avance de decisiones</strong>
              <span>{decidedDestinationCount} de {destinationIds.length} asignaturas de destino decididas</span>
            </div>
            <div className="ia-progress-track" role="progressbar" aria-label="Avance de decisiones del expediente"
              aria-valuemin={0} aria-valuemax={100} aria-valuenow={decisionProgressPercent}>
              <span style={{ width: `${decisionProgressPercent}%` }} />
            </div>
            <details><summary>Ver decisiones por destino</summary><ul className="ia-progress-list">{destinationIds.map(id => {
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
            })}</ul></details>
            {puedeEditar && <button className="students-primary" type="button" onClick={() => { void resolveExpediente(); }} disabled={!canResolve || resolving}>
              {resolving ? 'Resolviendo…' : 'Resolver expediente'}
            </button>}
            {!canResolve && puedeEditar && <small>Registra una decisión concluyente para cada asignatura de destino antes de resolver.</small>}
            {soloLectura && <small>Expediente resuelto: consulta disponible en solo lectura.</small>}
          </div>
          {displayAnalysis.advertencias.length > 0 && <details className="ia-warnings">
            <summary>Advertencias del análisis</summary>
            <ul>{displayAnalysis.advertencias.map((warning, index) => <li key={`${index}-${warning}`}>{warning}</li>)}</ul>
          </details>}
          {expandedKey == null ? null : Object.keys(proposalsByDestination).length === 0
            ? <p className="catalog-message">No hay recomendaciones viables en esta vista. Revisa los diagnósticos o agrega una agrupación con evidencia académica.</p>
            : <div className="ia-destinations" role="region" aria-label="Detalle de propuesta" tabIndex={-1} ref={detailPanelRef}><button type="button" className="students-secondary" onClick={() => setExpandedKey(null)}>Cerrar detalle</button>{Object.entries(proposalsByDestination).map(([destinationId, proposals]) => {
              const destination = destinationCourses.find(course => course.id === destinationId);
              return <section className="ia-destination" key={destinationId} aria-label={`Propuestas para ${proposals[0].nombreDestino}`}>
                <h4>{destination?.codigo ? `${destination.codigo} · ` : ''}{proposals[0].nombreDestino} - {destinationLabel}</h4>
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
                        <p>{originNames.length ? originNames.map(name => `${name} - ${originLabel}`).join(' + ') : 'Origen no disponible'}</p></div>
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
                        <button className="students-primary" type="button" onClick={() => acceptIaProposal(proposal)} disabled={savingReviewKey !== null || grouping || proposal.origenes.length === 0}>
                          {savingReviewKey === reviewKey ? 'Guardando…' : 'Confirmar resultado calculado'}
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
                        }} disabled={savingReviewKey !== null || grouping || proposal.origenes.length === 0}>
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
                            required={reviewMode === 'custom' || ![0, 4].includes(Number(rules.resultado))}
                            placeholder={reviewMode === 'accept' ? 'Justifica por qué aceptas esta propuesta no homologable.' : 'Explica brevemente por qué cambias o rechazas el resultado.'} />
                        </label>
                        {Number(rules.resultado) === 1 && <label className="ia-annotation-option">
                          <input type="checkbox" checked={recommendHomologar} onChange={event => setRecommendHomologar(event.target.checked)} />
                          <span>Es recomendable homologar (anotación; no cambia el resultado calculado)</span>
                        </label>}
                        <div className="ia-review-actions">
                          <button className="students-primary" type="submit" disabled={savingReviewKey !== null || (reviewMode === 'custom' && !reviewOutcome) || ((reviewMode === 'custom' || ![0, 4].includes(Number(rules.resultado))) && !reviewComment.trim())}>
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

function esRecomendable(proposal: PropuestaExpedienteIa): boolean {
  return [0, 4, 'Homologable', 'TransversalDirecta'].includes(proposal.evaluacion.reglas.resultado);
}

function esCercana(proposal: PropuestaExpedienteIa): boolean {
  const r = proposal.evaluacion.reglas;
  return [1, 2, 'RechazoPorCreditos', 'RechazoPorContenidos'].includes(r.resultado)
    && r.razonCreditos != null && r.razonCreditos >= .75 && r.coberturaContenidos != null && r.coberturaContenidos >= .75 && r.razonHoras != null;
}
function propuestaDePar(result: ResultadoParExpediente): PropuestaExpedienteIa {
  return { asignaturaDestinoId: result.asignaturaDestinoId, nombreDestino: result.nombreDestino,
    origenes: [{ asignaturaCursadaId: result.asignaturaCursadaId, nombre: result.nombreOrigen }],
    evaluacion: { ...result.evaluacion, cobertura: (result.evaluacion as PropuestaExpedienteIa['evaluacion']).cobertura
      ?? { porcentaje: result.evaluacion.reglas.coberturaContenidos, temasCoincidentes: [], temasAusentes: [] } } };
}
