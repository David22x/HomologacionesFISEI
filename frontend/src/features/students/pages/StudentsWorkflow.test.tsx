import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import StudentsPage from './StudentsPage';
import { catalogService } from '../../catalog/services/catalogService';
import { studentsService, type Estudiante } from '../studentsService';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const subjects = [1, 2, 3, 4].map(level => ({ id: `subject-${level}`, mallaVersionId: 'malla', nivelId: `level-${level}`, nivelNumero: level,
  codigo: `MAT${level}`, nombre: `Materia ${level}`, creditos: 3, horas: 48, unidadOrganizacional: null, esTransversal: false }));

function setup(students: Estudiante[] = []) {
  vi.spyOn(studentsService, 'listar').mockResolvedValue(students);
  vi.spyOn(catalogService, 'obtenerCarreras').mockResolvedValue([{ id: 'career', codigo: 'SW', nombre: 'Software', esDemo: false }]);
  vi.spyOn(catalogService, 'obtenerMallas').mockResolvedValue([{ id: 'malla', carreraId: 'career', version: '2026', vigente: true }]);
  vi.spyOn(catalogService, 'obtenerNiveles').mockResolvedValue(subjects.map(subject => ({ id: subject.nivelId, numero: subject.nivelNumero, mallaVersionId: 'malla' })));
  vi.spyOn(catalogService, 'obtenerAsignaturas').mockImplementation(async (_malla, nivel) => subjects.filter(subject => !nivel || subject.nivelId === nivel));
  render(<MemoryRouter><StudentsPage /></MemoryRouter>);
  return userEvent.setup();
}

it('carga niveles 1 a 3 y permite editar, quitar y guardar sin duplicar materias', async () => {
  const create = vi.spyOn(studentsService, 'crear').mockResolvedValue({} as Estudiante);
  const user = setup();
  await user.click(await screen.findByRole('button', { name: 'Registrar estudiante' }));
  await user.type(screen.getByLabelText('Cédula'), '0102030400');
  await user.type(screen.getByLabelText('Nombres'), 'Ana');
  await user.type(screen.getByLabelText('Apellidos'), 'Paz');
  await user.type(screen.getByLabelText('Carrera de origen'), 'Software · SW');
  await waitFor(() => expect(screen.getByLabelText('Malla')).toBeEnabled());
  await user.type(screen.getByLabelText('Malla'), 'Versión 2026 · vigente');
  await user.type(screen.getByLabelText('Nivel actual'), '3');
  await screen.findByLabelText('Nota de Materia 3');
  expect(screen.queryByLabelText('Nota de Materia 4')).not.toBeInTheDocument();
  expect(screen.getByLabelText('Nota de Materia 1')).toHaveValue(null);
  await user.click(screen.getAllByRole('button', { name: 'Quitar' })[2]);
  await user.clear(screen.getByLabelText('Créditos de Materia 1'));
  await user.type(screen.getByLabelText('Créditos de Materia 1'), '4');
  for (const level of [1, 2]) {
    await user.type(screen.getByLabelText(`Nota de Materia ${level}`), '8.5');
    await user.type(screen.getByLabelText(`Período de Materia ${level}`), '2026-A');
  }
  await user.click(screen.getByRole('button', { name: 'Registrar estudiante' }));
  await waitFor(() => expect(create).toHaveBeenCalledOnce());
  const history = create.mock.calls[0][0].historial;
  expect(history.mallaOrigenId).toBe('malla');
  expect(history.asignaturas).toHaveLength(2);
  expect(history.asignaturas[0].creditos).toBe(4);
});

it('elimina un estudiante después de la confirmación y actualiza el listado', async () => {
  const student = { id: 'student', cedula: '0102030400', tipo: 0, nombres: 'Ana', apellidos: 'Paz', historial: null } as Estudiante;
  const remove = vi.spyOn(studentsService, 'eliminar').mockResolvedValue();
  vi.spyOn(window, 'confirm').mockReturnValue(true);
  const user = setup([student]);
  await user.click(await screen.findByRole('button', { name: 'Eliminar' }));
  await waitFor(() => expect(remove).toHaveBeenCalledWith('student'));
  expect(await screen.findByText('Estudiante eliminado del registro activo.')).toBeInTheDocument();
});

it('recupera la malla al editar y agrega las materias faltantes conservando las notas existentes', async () => {
  const student = { id: 'student', cedula: '0102030400', tipo: 0, nombres: 'Ana', apellidos: 'Paz',
    historial: { carreraOrigenId: 'career', mallaOrigenId: 'malla', nivelActual: 2,
      asignaturas: [{ id: 'course', asignaturaId: 'subject-1', codigo: 'MAT1', nombre: 'Materia 1', creditos: 3, horas: 48, nota: 9, periodo: '2025-B' }] } } as Estudiante;
  const update = vi.spyOn(studentsService, 'actualizar').mockResolvedValue(student);
  const user = setup([student]);
  await user.click(await screen.findByRole('button', { name: 'Editar' }));
  await waitFor(() => expect(screen.getByLabelText('Malla')).toHaveValue('Versión 2026 · vigente'));
  expect(screen.getByLabelText('Nota de Materia 1')).toHaveValue(9);
  expect(screen.queryByLabelText('Nota de Materia 2')).not.toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Agregar materias de la malla hasta el nivel actual' }));
  await user.type(await screen.findByLabelText('Nota de Materia 2'), '8');
  await user.type(screen.getByLabelText('Período de Materia 2'), '2026-A');
  await user.click(screen.getByRole('button', { name: 'Guardar cambios' }));
  await waitFor(() => expect(update).toHaveBeenCalledOnce());
  expect(update.mock.calls[0][1].historial.asignaturas).toHaveLength(2);
  expect(update.mock.calls[0][1].historial.asignaturas[0].nota).toBe(9);
});
