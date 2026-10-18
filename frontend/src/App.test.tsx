
import { describe, it, expect } from 'vitest'
import { render } from '@testing-library/react'
import App from './App'

describe('Aplicación de homologación', () => {
  it('debe mostrar el contenido principal', () => {
    render(<App />)

    expect(
      document.body.textContent
    ).not.toBe('')
  })
})
