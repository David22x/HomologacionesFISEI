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
  it('crea un expediente y muestra su estado y detalles', async () => {
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
      carreraDestinoId: career.id,
      mallaDestinoId: malla.id,
      profesorId: 'teacher-1',
      profesorNombre: 'Docente',
      estado: 0,
      creadoEn: '2026-11-03T12:00:00Z',
      actualizadoEn: null,
    };

    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(200, [student]))
      .mockResolvedValueOnce(response(200, [career]))
      .mockResolvedValueOnce(response(200, []))
      .mockResolvedValueOnce(response(200, [malla]))
      .mockResolvedValueOnce(response(201, expediente))
      .mockResolvedValueOnce(response(200, [malla]));
    sessionStorage.setItem('accessToken', 'test-token');
    vi.stubGlobal('fetch', fetchMock);
    render(<MemoryRouter><ExpedientesPage canCreate /></MemoryRouter>);

    const user = userEvent.setup();
    await screen.findByText('0 expedientes');
    await user.type(screen.getByLabelText('Estudiante'), 'Paz, Ana · 0102030405');
    await user.type(screen.getByLabelText('Carrera de destino'), 'Ingeniería de Software · SW');
    await user.type(screen.getByLabelText('Malla curricular'), 'Versión 2026 · vigente');
    await user.click(screen.getByRole('button', { name: 'Crear expediente' }));

    expect(await screen.findByRole('heading', { name: 'Detalle del expediente' })).toBeInTheDocument();
    expect(screen.getAllByText('Borrador').length).toBeGreaterThan(0);
    expect(fetchMock).toHaveBeenNthCalledWith(
      5,
      'http://localhost:5080/api/v1/homologacion/expedientes',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          estudianteRefId: student.id,
          carreraDestinoRefId: career.id,
          mallaAplicableRefId: malla.id,
        }),
      }),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      'http://localhost:5080/api/v1/estudiantes',
      expect.objectContaining({ method: 'GET' }),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      'http://localhost:5080/api/v1/catalogo/carreras',
      expect.objectContaining({ method: 'GET' }),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      'http://localhost:5080/api/v1/homologacion/expedientes',
      expect.objectContaining({ method: 'GET' }),
    );
    expect(screen.getAllByText('Paz, Ana').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Ingeniería de Software').length).toBeGreaterThan(0);
    expect(screen.getByText('Versión 2026')).toBeInTheDocument();
  });
});
