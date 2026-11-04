import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ExpedientesPage from './ExpedientesPage';

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  vi.unstubAllGlobals();
});

function response(status: number, body: unknown) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  };
}

describe('Gestión de expedientes', () => {
  it('crea el expediente seleccionado y permite consultar y actualizar su estado', async () => {
    const student = {
      id: 'student-1',
      cedula: '0102030405',
      nombres: 'Ana',
      apellidos: 'Paz',
    };
    const career = {
      id: 'career-1',
      codigo: 'SW',
      nombre: 'Ingeniería de Software',
      esDemo: false,
    };
    const malla = {
      id: 'malla-1',
      carreraId: 'career-1',
      version: '2026',
      vigente: true,
    };
    const expediente = {
      id: 'expediente-1',
      estudianteId: 'student-1',
      cedulaEstudiante: student.cedula,
      nombresEstudiante: student.nombres,
      apellidosEstudiante: student.apellidos,
      carreraId: career.id,
      mallaId: malla.id,
      estado: 'Pendiente',
      creadoPor: 'admin',
      creadoEn: '2026-11-03T12:00:00Z',
      modificadoPor: 'admin',
      modificadoEn: '2026-11-03T12:00:00Z',
    };

    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(200, [student]))
      .mockResolvedValueOnce(response(200, [career]))
      .mockResolvedValueOnce(response(200, []))
      .mockResolvedValueOnce(response(200, [malla]))
      .mockResolvedValueOnce(response(201, expediente))
      .mockResolvedValueOnce(response(200, [malla]))
      .mockResolvedValueOnce(response(200, { ...expediente, estado: 'En revisión' }));
    sessionStorage.setItem('accessToken', 'test-token');
    vi.stubGlobal('fetch', fetchMock);
    render(<MemoryRouter><ExpedientesPage /></MemoryRouter>);

    const user = userEvent.setup();
    await screen.findByText('0 expedientes');
    await user.type(screen.getByLabelText('Estudiante'), 'Paz, Ana · 0102030405');
    await user.type(screen.getByLabelText('Carrera'), 'Ingeniería de Software · SW');
    await user.type(screen.getByLabelText('Malla curricular'), 'Versión 2026 · vigente');
    await user.click(screen.getByRole('button', { name: 'Crear expediente' }));

    expect(await screen.findByRole('heading', { name: 'Detalle del expediente' })).toBeInTheDocument();
    expect(screen.getAllByText('Pendiente').length).toBeGreaterThan(0);
    expect(fetchMock).toHaveBeenNthCalledWith(
      5,
      'http://localhost:5080/api/v1/expedientes',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ estudianteId: student.id, carreraId: career.id, mallaId: malla.id }),
      }),
    );

    await user.selectOptions(screen.getByLabelText('Actualizar estado'), 'En revisión');
    await user.click(screen.getByRole('button', { name: 'Guardar estado' }));

    expect(await screen.findByText('El estado del expediente se actualizó correctamente.')).toBeInTheDocument();
    expect(screen.getAllByText('En revisión').length).toBeGreaterThan(0);
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5080/api/v1/expedientes/expediente-1/estado',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ estado: 'En revisión' }),
      }),
    );
  });
});
