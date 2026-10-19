
import { useEffect, useState } from 'react';
import LoginPage from './features/auth/pages/LoginPage';
import {
  getCurrentUser,
  type AuthUser,
} from './features/auth/services/authService';
import './App.css';

function App() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [sessionError, setSessionError] = useState('');

  useEffect(() => {
    let active = true;

    async function restoreSession() {
      const token = sessionStorage.getItem('accessToken');

      if (!token) {
        if (active) setLoading(false);
        return;
      }

      try {
        const currentUser = await getCurrentUser(token);

        if (active) setUser(currentUser);
      } catch {
        sessionStorage.removeItem('accessToken');

        if (active) {
          setSessionError(
            'Tu sesión ha expirado. Inicia sesión nuevamente.'
          );
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    void restoreSession();

    return () => {
      active = false;
    };
  }, []);

  async function handleLogin(token: string) {
    const currentUser = await getCurrentUser(token);

    sessionStorage.setItem('accessToken', token);
    setUser(currentUser);
    setSessionError('');
  }

  function handleLogout() {
    sessionStorage.removeItem('accessToken');
    setUser(null);
  }

  if (loading) {
    return <main className="auth-page">Verificando sesión...</main>;
  }

  if (!user) {
    return (
      <>
        {sessionError && (
          <p className="session-error" role="alert">
            {sessionError}
          </p>
        )}
        <LoginPage onLogin={handleLogin} />
      </>
    );
  }

  return (
    <main className="home-page">
      <header className="home-header">
        <div className="brand">
          <span className="brand-mark">H</span>
          <span>Homologaciones FISEI</span>
        </div>

        <button className="logout-button" onClick={handleLogout}>
          Cerrar sesión
        </button>
      </header>

      <section className="welcome-card">
        <p className="eyebrow">PANEL PRINCIPAL</p>
        <h1>¡Bienvenido/a, {user.nombreCompleto}!</h1>
        <p>Has iniciado sesión correctamente.</p>

        <div className="role-section">
          <span>Rol(es) asignado(s)</span>
          <div className="role-list">
            {user.roles.map((role) => (
              <span className="role-badge" key={role}>
                {role}
              </span>
            ))}
          </div>
        </div>

        <p className="home-note">
          Accede a las funcionalidades del sistema según los permisos
          de tu cuenta.
        </p>
      </section>
    </main>
  );
}

export default App;
