import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import StudentsPage from './StudentsPage';

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  vi.unstubAllGlobals();
});

function renderStudentsPage(fetchMock: ReturnType<typeof vi.fn>) {
  sessionStorage.setItem('accessToken', 'test-token');
  vi.stubGlobal('fetch', fetchMock);
  return render(<MemoryRouter><StudentsPage /></MemoryRouter>);
}

function response(status: number, body: unknown) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  };
}

describe('Búsqueda de estudiantes por cédula', () => {
  it('muestra la ficha del estudiante y sus asignaturas al encontrarlo', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(200, []))
      .mockResolvedValueOnce(response(200, []))
      .mockResolvedValueOnce(response(200, {
        id: 'student-1',
        cedula: '0102030405',
        tipoIdentificacion: 0,
        nombres: 'Ana',
        apellidos: 'Paz',
        tipo: 0,
        creadoPor: 'admin',
        creadoEn: '2026-10-31T12:00:00Z',
        modificadoPor: 'admin',
        modificadoEn: '2026-10-31T12:00:00Z',
        historial: {
          id: 'history-1',
          estudianteId: 'student-1',
          carreraOrigenId: null,
          carreraOrigenNombre: 'Ingeniería de Software',
          institucionOrigen: null,
          nivelActual: 3,
          actualizadoEn: '2026-10-31T12:00:00Z',
          asignaturas: [{
            id: 'course-1',
            asignaturaId: 'subject-1',
            codigo: 'MAT101',
            nombre: 'Matemática',
            creditos: 3,
            horas: 48,
            nota: 9.5,
            periodo: '2026-A',
          }],
        },
      }));
    renderStudentsPage(fetchMock);

    const user = userEvent.setup();
    await screen.findByText('0 estudiantes registrados');
    await user.type(screen.getByLabelText('Cédula del estudiante'), '0102030405');
    await user.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(await screen.findByRole('heading', { name: 'Paz, Ana' })).toBeInTheDocument();
    expect(screen.getByText(/Ingeniería de Software/)).toBeInTheDocument();
    expect(screen.getByText('MAT101')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5080/api/v1/estudiantes/cedula/0102030405',
      expect.objectContaining({ method: 'GET' }),
    );
  });

  it('muestra un mensaje uniforme cuando la API no encuentra el estudiante', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(200, []))
      .mockResolvedValueOnce(response(200, []))
      .mockResolvedValueOnce(response(404, {}));
    renderStudentsPage(fetchMock);

    const user = userEvent.setup();
    await screen.findByText('0 estudiantes registrados');
    await user.type(screen.getByLabelText('Cédula del estudiante'), '0102030405');
    await user.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(await screen.findByRole('status')).toHaveTextContent(
      'No se encontró un estudiante con esa cédula.',
    );
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
