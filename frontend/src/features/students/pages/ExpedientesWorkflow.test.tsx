import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ExpedientesPage from './ExpedientesPage';
import { catalogService } from '../../catalog/services/catalogService';
import { studentsService, type Estudiante } from '../studentsService';
import { expedientesService, type AnalisisExpedienteIa, type Expediente } from '../expedientesService';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

it('excluye carrera de origen, identifica SW a TI y oculta los diagnósticos inicialmente', async () => {
  const student = { id: 'student', cedula: '0102030400', nombres: 'Ana', apellidos: 'Paz',
    historial: { carreraOrigenId: 'sw', asignaturas: [{ id: 'origin', asignaturaId: 'source', nombre: 'Física', codigo: 'FIS', creditos: 4, horas: 64, nota: 8, periodo: '2026-A' }] } } as unknown as Estudiante;
  const expediente = { id: 'exp', estudianteId: 'student', carreraDestinoId: 'ti', mallaDestinoId: 'malla',
    estado: 'AnalizadoConIa', creadoEn: '2026-11-22T10:00:00Z' } as Expediente;
  const proposal = (id: string, nombre: string, resultado: number) => ({ asignaturaDestinoId: id, nombreDestino: nombre,
    origenes: [{ asignaturaCursadaId: 'origin', nombre: 'Física' }],
    evaluacion: { reglas: { resultado, razonCreditos: 1, coberturaContenidos: id === 'history' ? .5 : id === 'near' ? .77 : .9, razonHoras: 1, advertencias: [], versionReglas: '1' },
      cobertura: { porcentaje: .9, temasCoincidentes: [], temasAusentes: [] } } });
  vi.spyOn(studentsService, 'listar').mockResolvedValue([student]);
  vi.spyOn(catalogService, 'obtenerCarreras').mockResolvedValue([
    { id: 'sw', codigo: 'SW', nombre: 'Software', esDemo: false }, { id: 'ti', codigo: 'TI', nombre: 'Tecnologías', esDemo: false }]);
  vi.spyOn(catalogService, 'obtenerMallas').mockResolvedValue([]);
  vi.spyOn(catalogService, 'obtenerNiveles').mockResolvedValue([]);
  vi.spyOn(expedientesService, 'listar').mockResolvedValue([expediente]);
  const precargar = vi.spyOn(expedientesService, 'precargarPares').mockResolvedValue([{ id: 'pair', expedienteId: 'exp', asignaturaCursadaId: 'origin', asignaturaDestinoId: 'physics', precargado: true }]);
  vi.spyOn(expedientesService, 'listarPares').mockResolvedValue([]);
  vi.spyOn(expedientesService, 'listarDecisiones').mockResolvedValue([]);
  let selected: { asignaturaDestinoId: string; origenIds: string[] }[] = [];
  const result = { parId: 'pair', asignaturaCursadaId: 'origin', nombreOrigen: 'Física', asignaturaDestinoId: 'physics', nombreDestino: 'Física', evaluacion: proposal('physics', 'Física', 0).evaluacion };
  vi.spyOn(expedientesService, 'consultarSeleccion').mockImplementation(async () => ({ seleccion: selected, resultados: [result] }));
  vi.spyOn(expedientesService, 'guardarSeleccion').mockImplementation(async (_id, value) => { selected = value; return value; });
  vi.spyOn(expedientesService, 'consultarAnalisis').mockResolvedValue({ id: 'analysis', estado: 'AnalizadoConIa', ejecutadoEn: '2026-11-22T10:00:00Z',
    advertencias: [], propuestas: [proposal('physics', 'Física', 0), proposal('history', 'Historia', 2), proposal('near', 'Física aplicada', 2)] } as AnalisisExpedienteIa);
  render(<MemoryRouter><ExpedientesPage canCreate /></MemoryRouter>);
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Estudiante'), 'Paz, Ana · 0102030400');
  const options = () => [...document.querySelectorAll('#expediente-career-options option')].map(option => option.getAttribute('value'));
  await waitFor(() => expect(options()).not.toContain('Software · SW'));
  expect(options()).toContain('Tecnologías · TI');
  await user.click(screen.getByRole('button', { name: 'Ver expediente' }));
  expect(await screen.findByLabelText('Cambio de carrera')).toHaveTextContent('SW');
  expect(screen.getByLabelText('Cambio de carrera')).toHaveTextContent('TI');
  const table = await screen.findByRole('table', { name: 'Propuestas de IA' });
  expect(within(table).getByText('Física - TI')).toBeInTheDocument();
  expect(precargar).toHaveBeenCalledWith('exp');
  expect(screen.queryByRole('columnheader', { name: 'Umbral' })).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: /Revisar siguiente pendiente/ })).toBeEnabled();
  expect(within(table).getByText('Cercana al umbral: revisión docente')).toBeInTheDocument();
  await user.click(screen.getByRole('checkbox', { name: 'Seleccionar coincidencia Física' }));
  await waitFor(() => expect(screen.getByRole('checkbox', { name: 'Seleccionar propuesta IA Física' })).toBeChecked());
  expect(screen.getByRole('checkbox', { name: 'Retirar selección Física' })).toBeChecked();
  expect(document.querySelector('.selection-dock')).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Contraer selección' }));
  expect(document.getElementById('definitive-table')).not.toBeVisible();
  await user.click(screen.getByRole('button', { name: 'Ampliar selección' }));
  expect(screen.getByRole('checkbox', { name: 'Retirar selección Física' })).toBeChecked();

  expect(screen.queryByRole('heading', { level: 4, name: 'Física - TI' })).not.toBeInTheDocument();
  await user.click(within(table).getAllByRole('button', { name: 'Ver detalle' })[0]);
  expect(screen.getByRole('heading', { level: 4, name: 'Física - TI' })).toBeInTheDocument();
  expect(screen.queryByRole('heading', { level: 4, name: 'Historia - TI' })).not.toBeInTheDocument();
  await user.click(screen.getByRole('checkbox', { name: /Mostrar destinos sin recomendación/ }));
  expect(within(table).getByText('Historia - TI')).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Ver expediente' }));
  await waitFor(() => expect(screen.getByRole('checkbox', { name: 'Seleccionar coincidencia Física' })).toBeChecked());
  await user.click(screen.getByRole('checkbox', { name: 'Seleccionar propuesta IA Física' }));
  await waitFor(() => expect(screen.getByRole('checkbox', { name: 'Seleccionar coincidencia Física' })).not.toBeChecked());
  expect(screen.queryByRole('checkbox', { name: 'Retirar selección Física' })).not.toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: /Revisar siguiente pendiente/ }));
  expect(screen.getByRole('region', { name: 'Detalle de propuesta' })).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Cerrar expediente' }));
  expect(document.querySelector('.selection-dock')).not.toBeInTheDocument();
  expect(document.querySelector('.has-selection-dock')).not.toBeInTheDocument();

});
