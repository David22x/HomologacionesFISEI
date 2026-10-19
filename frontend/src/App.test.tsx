import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from './App';

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  vi.unstubAllGlobals();
});

describe('Autenticación', () => {
  it('inicia sesión por la API y muestra el nombre y el rol', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          accessToken: 'jwt-de-prueba',
          tokenType: 'Bearer',
          expiresAt: '2026-10-19T15:00:00Z',
          user: {
            id: 'usuario-1',
            email: 'profesor@uta.edu.ec',
            nombreCompleto: 'Docente de Prueba',
            roles: ['Profesor'],
          },
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          id: 'usuario-1',
          email: 'profesor@uta.edu.ec',
          nombreCompleto: 'Docente de Prueba',
          roles: ['Profesor'],
        }),
      });
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    render(<App />);

    await user.type(screen.getByLabelText('Correo electrónico'), 'profesor@uta.edu.ec');
    await user.type(screen.getByLabelText('Contraseña'), 'clave-de-prueba');
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión' }));

    expect(
      await screen.findByRole('heading', { name: '¡Bienvenido/a, Docente de Prueba!' })
    ).toBeInTheDocument();
    expect(screen.getByText('Profesor')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      'http://localhost:5080/auth/login',
      expect.objectContaining({ method: 'POST' })
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      'http://localhost:5080/auth/me',
      expect.objectContaining({
        headers: { Authorization: 'Bearer jwt-de-prueba' },
      })
    );
  });

  it('muestra un mensaje genérico cuando las credenciales no son válidas', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: false, status: 401 })
    );

    const user = userEvent.setup();
    render(<App />);

    await user.type(screen.getByLabelText('Correo electrónico'), 'alguien@uta.edu.ec');
    await user.type(screen.getByLabelText('Contraseña'), 'incorrecta');
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión' }));

    expect(
      await screen.findByRole('alert')
    ).toHaveTextContent('No se pudo iniciar sesión. Verifica tus credenciales e intenta nuevamente.');
    expect(sessionStorage.getItem('accessToken')).toBeNull();
  });
});
