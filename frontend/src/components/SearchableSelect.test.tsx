import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { expect, it, vi } from 'vitest';
import SearchableSelect from './SearchableSelect';

const options = [{ value: 'sw', label: 'Software' }, { value: 'ti', label: 'Tecnologías de la Información' }];

it('limpia la etiqueta cuando el formulario restablece la selección', () => {
  const props = { id: 'carrera', label: 'Carrera', options, placeholder: 'Buscar', onChange: vi.fn() };
  const { rerender } = render(<SearchableSelect {...props} value="sw" />);
  expect(screen.getByLabelText('Carrera')).toHaveValue('Software');
  rerender(<SearchableSelect {...props} value="" />);
  expect(screen.getByLabelText('Carrera')).toHaveValue('');
});

it('el texto sin coincidencia no conserva un identificador anterior', () => {
  function Form() {
    const [value, setValue] = useState('sw');
    return <><SearchableSelect id="carrera" label="Carrera" options={options} placeholder="Buscar" value={value} onChange={setValue} /><output aria-label="Selección">{value}</output></>;
  }
  render(<Form />);
  fireEvent.change(screen.getByLabelText('Carrera'), { target: { value: 'Otra carrera' } });
  expect(screen.getByLabelText('Selección')).toBeEmptyDOMElement();
  expect(screen.getByLabelText('Carrera')).toHaveValue('Otra carrera');
  fireEvent.change(screen.getByLabelText('Carrera'), { target: { value: 'Software' } });
  expect(screen.getByLabelText('Selección')).toHaveTextContent('sw');
});
