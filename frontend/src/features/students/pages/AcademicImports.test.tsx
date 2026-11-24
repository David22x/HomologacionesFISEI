import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import RecordImportPage from './RecordImportPage';
import ImportHistoryPage from './ImportHistoryPage';
import { studentsService, type Estudiante } from '../studentsService';
import { catalogService } from '../../catalog/services/catalogService';
import * as files from '../services/fileImports';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
const student = { id: 'student', cedula: '0102030400', tipoIdentificacion: 0, tipo: 1, nombres: 'Ana', apellidos: 'Paz',
  historial: { carreraOrigenId: null, institucionOrigen: 'Universidad origen', carreraOrigenNombre: 'Software', nivelActual: 3,
    asignaturas: [{ codigo: 'PROG', nombre: 'Programacion', creditos: 3, horas: 48, nota: 9, periodo: '2026-A', temas: [] }] } } as unknown as Estudiante;

it('impide confirmar un PDF de otra persona y conserva las filas revisadas al importar', async () => {
  vi.spyOn(studentsService, 'listar').mockResolvedValue([student]);
  vi.spyOn(catalogService, 'obtenerCarreras').mockResolvedValue([]);
  const upload = vi.spyOn(files, 'uploadImport').mockResolvedValueOnce({ identificacion: '1111111111', advertencias: [],
    filas: [{ nombre: 'Programacion', carrera: 'Software', periodo: '2026-A', nota: 9, estado: 'APROBADO', pagina: 1, seleccionado: true,
      asignaturaId: null, creditos: null, horas: null, reemplazar: false }] }).mockResolvedValueOnce({});
  const user = userEvent.setup(); render(<MemoryRouter><RecordImportPage /></MemoryRouter>);
  await user.selectOptions(screen.getByLabelText('Operación'), 'actualizar');
  await user.selectOptions(await screen.findByLabelText('Estudiante del récord'), 'student');
  await user.upload(screen.getByLabelText('Archivo PDF'), new File(['%PDF-prueba'], 'record.pdf', { type: 'application/pdf' }));
  await user.click(screen.getByRole('button', { name: 'Leer récord académico' }));
  const confirm = await screen.findByRole('button', { name: 'Confirmar materias seleccionadas' });
  expect(confirm).toBeDisabled();
  await user.clear(screen.getByLabelText('Identificación del documento')); await user.type(screen.getByLabelText('Identificación del documento'), student.cedula);
  await user.type(screen.getByLabelText('Créditos fila 1'), '3'); await user.type(screen.getByLabelText('Horas fila 1'), '48');
  await user.click(confirm);
  await waitFor(() => expect(upload).toHaveBeenCalledTimes(2));
  expect(upload.mock.calls[1][2]).toMatchObject({ estudianteId: 'student', filas: [{ nota: 9, creditos: 3, horas: 48 }] });
});

it('solicita temas pendientes y permite completar el historial externo sin volver a subir XLSX', async () => {
  vi.spyOn(studentsService, 'obtener').mockResolvedValue(student);
  const save = vi.spyOn(studentsService, 'actualizarExterno').mockResolvedValue(student);
  const user = userEvent.setup(); render(<MemoryRouter initialEntries={['/estudiantes/importar?estudiante=student']}><ImportHistoryPage /></MemoryRouter>);
  expect(await screen.findByText(/Programacion: faltan temas y subtemas/)).toBeInTheDocument();
  await user.click(screen.getByText('Temas y subtemas (0)')); await user.click(screen.getByRole('button', { name: 'Agregar tema' }));
  await user.type(screen.getByLabelText('Tema 1 de Programacion'), 'Clases');
  await user.type(screen.getByLabelText('Subtemas de Clases'), 'Objetos\nHerencia');
  await user.click(screen.getByRole('button', { name: 'Guardar historial externo' }));
  await waitFor(() => expect(save).toHaveBeenCalledOnce());
  expect(save.mock.calls[0][1]).toMatchObject({ historial: { asignaturas: [{ temas: [{ nombre: 'Clases', subtemas: ['Objetos', 'Herencia'] }] }] } });
});

it('registra un externo desde PDF y solicita la escala antes de usar su nota original', async () => {
  vi.spyOn(studentsService, 'listar').mockResolvedValue([]); vi.spyOn(catalogService, 'obtenerCarreras').mockResolvedValue([]);
  const upload = vi.spyOn(files, 'uploadImport').mockResolvedValueOnce({ identificacion: 'AB12345', advertencias: [],
    perfil: { nombres: 'Ana', apellidos: 'Paz', nombreCompleto: 'Ana Paz', carrera: 'Computing', institucion: 'Universidad origen', tipoIdentificacion: 1, escalaMaxima: null },
    filas: [{ nombre: 'Programming', carrera: 'Computing', periodo: '2026-A', nota: null, notaOriginal: 85, escalaOriginal: null, estado: 'APROBADO', pagina: 1, seleccionado: false,
      asignaturaId: null, creditos: null, horas: null, reemplazar: false }] }).mockResolvedValueOnce({ id: 'nuevo' });
  const user = userEvent.setup(); render(<MemoryRouter><RecordImportPage /></MemoryRouter>);
  expect(screen.queryByLabelText('Estudiante del récord')).not.toBeInTheDocument();
  await user.selectOptions(screen.getByLabelText('Origen del documento'), 'externo');
  await user.upload(screen.getByLabelText('Archivo PDF'), new File(['%PDF'], 'record.pdf', { type: 'application/pdf' }));
  await user.click(screen.getByRole('button', { name: 'Leer récord académico' }));
  expect(await screen.findByLabelText('Nombres del estudiante')).toHaveValue('Ana');
  expect(screen.getByLabelText('Apellidos del estudiante')).toHaveValue('Paz');
  expect(screen.getByLabelText('Nota fila 1')).toHaveValue(null);
  await user.type(screen.getByLabelText('Escala máxima del reporte (solo si está verificada)'), '100');
  expect(screen.getByLabelText('Nota fila 1')).toHaveValue(8.5);
  await user.click(screen.getByLabelText('Importar Programming'));
  await user.click(screen.getByRole('button', { name: 'Confirmar registro y calificaciones' }));
  await waitFor(() => expect(upload).toHaveBeenCalledTimes(2));
  expect(upload.mock.calls[1][0]).toBe('/api/v1/documentos/records/registrar');
  expect(upload.mock.calls[1][2]).toMatchObject({ tipo: 1, documento: { identificacion: 'AB12345', perfil: { nombres: 'Ana', apellidos: 'Paz' }, filas: [{ nota: 8.5, creditos: null, horas: null }] } });
});

it('precarga carrera malla y asignaturas de un estudiante nuevo sin pedir su registro previo', async () => {
  vi.spyOn(studentsService, 'listar').mockResolvedValue([]);
  vi.spyOn(catalogService, 'obtenerCarreras').mockResolvedValue([{ id: 'career', nombre: 'Software', codigo: 'SW', esDemo: false }]);
  vi.spyOn(catalogService, 'obtenerMallas').mockResolvedValue([{ id: 'malla', carreraId: 'career', version: '2026', vigente: true, estado: 'Publicada' }]);
  vi.spyOn(catalogService, 'obtenerAsignaturas').mockResolvedValue([{ id: 'subject', nombre: 'Programacion', codigo: 'PROG', nivelNumero: 2, mallaVersionId: 'malla', nivelId: 'level', creditos: 3, horas: 48, unidadOrganizacional: null, esTransversal: false }]);
  const upload = vi.spyOn(files, 'uploadImport').mockResolvedValueOnce({ identificacion: '0102030400', advertencias: [],
    perfil: { nombres: 'Ana', apellidos: 'Paz', nombreCompleto: 'Ana Paz', carrera: 'Software', institucion: 'UTA', tipoIdentificacion: 0, nivelActual: null },
    filas: [{ nombre: 'Programacion', carrera: 'Software', periodo: '2026-A', nota: 9, estado: 'APROBADO', pagina: 1, seleccionado: true, asignaturaId: null, creditos: 3, horas: null, reemplazar: false }] }).mockResolvedValueOnce({});
  const user = userEvent.setup(); render(<MemoryRouter><RecordImportPage /></MemoryRouter>);
  await waitFor(() => expect(catalogService.obtenerCarreras).toHaveBeenCalled());
  await user.upload(screen.getByLabelText('Archivo PDF'), new File(['%PDF'], 'record.pdf', { type: 'application/pdf' }));
  await user.click(screen.getByRole('button', { name: 'Leer récord académico' }));
  await waitFor(() => expect(screen.getByLabelText('Catálogo fila 1')).toHaveValue('subject'));
  expect(screen.getByLabelText('Carrera del documento')).toHaveValue('career'); expect(screen.getByLabelText('Malla para asociar')).toHaveValue('malla');
  expect(screen.getByLabelText('Nivel actual (verificar)')).toHaveValue(2);
  await user.click(screen.getByRole('button', { name: 'Confirmar registro y calificaciones' }));
  await waitFor(() => expect(upload).toHaveBeenCalledTimes(2));
  expect(upload.mock.calls[1][2]).toMatchObject({ tipo: 0, carreraOrigenId: 'career', mallaOrigenId: 'malla', nivelActual: 2, documento: { filas: [{ asignaturaId: 'subject' }] } });
});
