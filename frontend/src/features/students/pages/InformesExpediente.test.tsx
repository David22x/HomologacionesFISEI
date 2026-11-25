import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import InformesExpediente from './InformesExpediente';
import { expedientesService, type VersionInforme } from '../expedientesService';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const version = (numero: number): VersionInforme => ({
  id: `informe-${numero}`, version: numero, generadoEn: '2026-11-23T10:00:00Z', huellaSha256: 'abc',
});

it('muestra las versiones y genera una nueva', async () => {
  const listar = vi.spyOn(expedientesService, 'listarInformes')
    .mockResolvedValueOnce([version(1)])
    .mockResolvedValueOnce([version(2), version(1)]);
  const generar = vi.spyOn(expedientesService, 'generarInforme').mockResolvedValue(version(2));
  render(<InformesExpediente expedienteId="exp" puedeGenerar />);
  expect(await screen.findByText(/Versión 1/)).toBeInTheDocument();
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar informe' }));
  expect(await screen.findByText('Se generó la versión 2 del informe.')).toBeInTheDocument();
  expect(generar).toHaveBeenCalledWith('exp');
  expect(listar).toHaveBeenCalledTimes(2);
  expect(screen.getByRole('button', { name: 'Descargar versión 2' })).toBeInTheDocument();
});

it('avisa cuando regenerar no crea una versión nueva', async () => {
  vi.spyOn(expedientesService, 'listarInformes').mockResolvedValue([version(1)]);
  vi.spyOn(expedientesService, 'generarInforme').mockResolvedValue(version(1));
  render(<InformesExpediente expedienteId="exp" puedeGenerar />);
  await screen.findByText(/Versión 1/);
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar informe' }));
  expect(await screen.findByText(/no se creó una versión nueva/)).toBeInTheDocument();
});

it('permite descargar pero no generar a quien no es profesor', async () => {
  vi.spyOn(expedientesService, 'listarInformes').mockResolvedValue([version(1)]);
  const descargar = vi.spyOn(expedientesService, 'descargarInforme').mockResolvedValue(new Blob(['%PDF-']));
  let nombreDescarga = '';
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    nombreDescarga = this.download;
    expect(this.href).toBe('blob:informe');
    expect(document.body.contains(this)).toBe(true);
  });
  URL.createObjectURL = vi.fn(() => 'blob:informe');
  URL.revokeObjectURL = vi.fn();
  render(<InformesExpediente expedienteId="exp" puedeGenerar={false} />);
  await screen.findByText(/Versión 1/);
  expect(screen.queryByRole('button', { name: 'Generar informe' })).not.toBeInTheDocument();
  await userEvent.setup().click(screen.getByRole('button', { name: 'Descargar versión 1' }));
  await waitFor(() => expect(descargar).toHaveBeenCalledWith('exp', 1));
  expect(click).toHaveBeenCalledOnce();
  expect(nombreDescarga).toBe('homologacion-exp-v1.pdf');
  expect(document.querySelector('a[download]')).not.toBeInTheDocument();
});

it('muestra el error cuando no se puede generar', async () => {
  vi.spyOn(expedientesService, 'listarInformes').mockResolvedValue([]);
  vi.spyOn(expedientesService, 'generarInforme').mockRejectedValue(new Error('El informe requiere un expediente resuelto con decisiones.'));
  render(<InformesExpediente expedienteId="exp" puedeGenerar />);
  expect(await screen.findByText('Aún no hay informes generados para este expediente.')).toBeInTheDocument();
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar informe' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('El informe requiere un expediente resuelto');
});
