import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { expect, it, vi } from 'vitest';
import StudentsPage from './StudentsPage';
import { studentsService, type Estudiante } from '../studentsService';
import { catalogService } from '../../catalog/services/catalogService';

vi.mock('../studentsService', () => ({ studentsService: { listar: vi.fn() } }));
vi.mock('../../catalog/services/catalogService', () => ({ catalogService: {
  obtenerCarreras: vi.fn(), obtenerMallas: vi.fn(), obtenerNiveles: vi.fn(), obtenerAsignaturas: vi.fn(),
} }));

it('permite reabrir la edición del mismo estudiante y agregar una materia de su malla', async () => {
  vi.mocked(studentsService.listar).mockResolvedValue([{
    id: 'alumno', cedula: '1801234567', nombres: 'Ana', apellidos: 'Paz', tipo: 0,
    historial: { carreraOrigenId: 'sw', mallaOrigenId: 'malla', nivelActual: 1, asignaturas: [] },
  } as unknown as Estudiante]);
  vi.mocked(catalogService.obtenerCarreras).mockResolvedValue([{ id: 'sw', codigo: 'SW', nombre: 'Software', esDemo: false }]);
  vi.mocked(catalogService.obtenerMallas).mockResolvedValue([{ id: 'malla', carreraId: 'sw', version: '2026', vigente: true }]);
  vi.mocked(catalogService.obtenerNiveles).mockResolvedValue([{ id: 'nivel', mallaVersionId: 'malla', numero: 1 }]);
  vi.mocked(catalogService.obtenerAsignaturas).mockResolvedValue([{ id: 'fisica', mallaVersionId: 'malla', nivelId: 'nivel', nivelNumero: 1,
    codigo: 'FIS', nombre: 'Física', creditos: 4, horas: 64, unidadOrganizacional: null, esTransversal: false }]);
  render(<MemoryRouter><StudentsPage /></MemoryRouter>);
  fireEvent.click(await screen.findByRole('button', { name: 'Editar' }));
  await waitFor(() => expect(screen.getByLabelText('Malla')).toHaveValue('Versión 2026 · vigente'));
  fireEvent.click(screen.getByRole('button', { name: 'Editar' }));
  expect(screen.getByLabelText('Malla')).toHaveValue('Versión 2026 · vigente');
  await waitFor(() => expect(screen.getByLabelText('Nivel')).not.toBeDisabled());
  fireEvent.change(screen.getByLabelText('Nivel'), { target: { value: 'Nivel 1' } });
  await waitFor(() => expect(catalogService.obtenerAsignaturas).toHaveBeenCalledWith('malla', 'nivel', ''));
  fireEvent.change(screen.getByLabelText('Asignatura'), { target: { value: 'FIS · Física' } });
  fireEvent.change(screen.getByLabelText('Nota'), { target: { value: '8' } });
  fireEvent.change(screen.getByLabelText('Período'), { target: { value: '2026-A' } });
  fireEvent.click(screen.getByRole('button', { name: 'Agregar asignatura' }));
  expect(screen.getByLabelText('Nombre de FIS')).toHaveValue('Física');
});
