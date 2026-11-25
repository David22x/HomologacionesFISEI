import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import ServiceAvailability from './ServiceAvailability';
import { publishAvailability } from '../lib/serviceFetch';

afterEach(() => vi.unstubAllGlobals());

it('informa servicios afectados y retira el aviso cuando una consulta confirma su recuperación', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ services: [
    { service: 'Students', available: true }, { service: 'Academic', available: true },
  ] }), { status: 200 }));
  vi.stubGlobal('fetch', fetch);
  render(<ServiceAvailability />);
  act(() => publishAvailability({ service: 'Students', available: false, message: 'Estudiantes temporalmente no disponible.' }));
  expect(screen.getByRole('status')).toBeInTheDocument();
  await userEvent.setup().click(screen.getByRole('button', { name: 'Comprobar servicios' }));
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
  expect(fetch).toHaveBeenCalledTimes(1);
  expect(fetch.mock.calls[0][0]).toContain('/status/services');
});
