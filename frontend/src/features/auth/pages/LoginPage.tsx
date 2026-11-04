
import { useState, type FormEvent } from 'react';
import { login } from '../services/authService';

interface LoginPageProps {
  onLogin: (token: string) => Promise<void>;
}

export default function LoginPage({ onLogin }: LoginPageProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');

    if (!email.trim() || !password) {
      setError('Ingresa tu correo y contraseña.');
      return;
    }

    setLoading(true);

    try {
      const result = await login(email.trim(), password);
      await onLogin(result.accessToken);
    } catch {
      setError(
        'No se pudo iniciar sesión. Verifica tus credenciales e intenta nuevamente.'
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-card">
        <div className="brand">
          <span className="brand-mark">H</span>
          <span>Homologaciones FISEI</span>
        </div>

        <p className="eyebrow">SISTEMA ACADÉMICO</p>
        <h1>Iniciar sesión</h1>
        <p className="description">
          Ingresa tus credenciales para acceder al sistema.
        </p>

        <form onSubmit={handleSubmit}>
          <label htmlFor="email">Correo electrónico</label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            placeholder="usuario@uta.edu.ec"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            disabled={loading}
            required
          />

          <label htmlFor="password">Contraseña</label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            placeholder="Ingresa tu contraseña"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            disabled={loading}
            required
          />

          {error && (
            <p className="auth-error" role="alert">
              {error}
            </p>
          )}

          <button type="submit" disabled={loading}>
            {loading ? 'Verificando...' : 'Iniciar sesión'}
          </button>
        </form>

        <p className="auth-footer">
          Sistema de Homologación de Materias y Créditos
        </p>
      </section>
    </main>
  );
}
