import { ServiceUnavailableError } from './lib/serviceFetch';
import RecordImportPage from './features/students/pages/RecordImportPage';

import { useEffect, useState } from 'react';
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
} from 'react-router-dom';

import LoginPage from './features/auth/pages/LoginPage';
import {
  getCurrentUser,
  type AuthUser,
} from './features/auth/services/authService';

import { onSessionExpired } from './features/auth/services/sessionEvents';
import ProtectedRoute from './components/ProtectedRoute';
import AppLayout from './components/AppLayout';
import HomePage from './features/auth/pages/HomePage';
import AccessDeniedPage from './features/auth/pages/AccessDeniedPage';
import AdminPage from './features/auth/pages/AdminPage';
import './App.css';
import CatalogPage from './features/catalog/pages/CatalogPage';
import CatalogMaintenancePage from './features/catalog/pages/CatalogMaintenancePage';
import TemasPage from './features/catalog/pages/TemasPage';
import StudentsPage from './features/students/pages/StudentsPage';
import ImportHistoryPage from './features/students/pages/ImportHistoryPage';
import ExpedientesPage from './features/students/pages/ExpedientesPage';

function App() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [sessionError, setSessionError] = useState('');
  const [sessionUnavailable, setSessionUnavailable] = useState(false);
  const [sessionAttempt, setSessionAttempt] = useState(0);

  useEffect(() => {
    let active = true;

    async function restoreSession() {
      setLoading(true); setSessionUnavailable(false);
      const token = sessionStorage.getItem('accessToken');

      if (!token) {
        if (active) setLoading(false);
        return;
      }

      try {
        const currentUser = await getCurrentUser(token);
        if (active) setUser(currentUser);
      } catch (cause) {
        if (cause instanceof ServiceUnavailableError) {
          if (active) { setSessionUnavailable(true); setSessionError(cause.message); }
          return;
        }
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
  }, [sessionAttempt]);

  useEffect(() => {
    return onSessionExpired(() => {
      sessionStorage.removeItem('accessToken');
      setUser(null);
      setSessionError('Tu sesión ha expirado. Inicia sesión nuevamente.');
    });
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

  if (sessionUnavailable) return <main className="session-recovery"><h1>Autenticación temporalmente no disponible</h1><p role="alert">{sessionError}</p><p>Tu sesión se conserva. Debemos validarla antes de mostrar información protegida.</p><button type="button" className="students-primary" onClick={() => setSessionAttempt(value => value + 1)}>Reintentar conexión</button></main>;

  return (
    <BrowserRouter>
      {sessionError && !user && (
        <p className="session-error" role="alert">
          {sessionError}
        </p>
      )}

      <Routes>
        <Route
          path="/login"
          element={
            user
              ? <Navigate to="/home" replace />
              : <LoginPage onLogin={handleLogin} />
          }
        />

        <Route
          path="/"
          element={
            <Navigate to={user ? '/home' : '/login'} replace />
          }
        />

        <Route
          element={
            <ProtectedRoute user={user}>
              {user && <AppLayout user={user} onLogout={handleLogout} />}
            </ProtectedRoute>
          }
        >
          <Route
            path="/home"
            element={user && <HomePage user={user} />}
          />

          <Route
            path="/catalogo"
            element={
              <ProtectedRoute user={user} allowedRoles={['Administrador', 'Profesor']}>
                <CatalogPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/admin"
            element={
              <ProtectedRoute user={user} allowedRoles={['Administrador']}>
                <AdminPage currentUserId={user?.id ?? ''} />
              </ProtectedRoute>
            }
          />

          <Route
            path="/estudiantes"
            element={
              <ProtectedRoute user={user} allowedRoles={['Administrador', 'Profesor']}>
                <StudentsPage />
              </ProtectedRoute>
            }
          />

          <Route path="/estudiantes/record-pdf" element={<ProtectedRoute user={user} allowedRoles={['Administrador', 'Profesor']}><RecordImportPage /></ProtectedRoute>} />
          <Route
            path="/estudiantes/importar"
            element={
              <ProtectedRoute user={user} allowedRoles={['Administrador', 'Profesor']}>
                <ImportHistoryPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/expedientes"
            element={
              <ProtectedRoute user={user} allowedRoles={['Administrador', 'Profesor']}>
                <ExpedientesPage canCreate={Boolean(user?.roles.includes('Profesor'))} />
              </ProtectedRoute>
            }
          />

          <Route
            path="/catalogo/mantenimiento"
            element={
              <ProtectedRoute user={user} allowedRoles={['Administrador']}>
                <CatalogMaintenancePage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/catalogo/asignaturas/:asignaturaId/temas"
            element={
              <ProtectedRoute user={user} allowedRoles={['Administrador', 'Profesor']}>
                <TemasPage isAdmin={Boolean(user?.roles.includes('Administrador'))} />
              </ProtectedRoute>
            }
          />

          <Route path="/acceso-denegado" element={<AccessDeniedPage />} />
        </Route>

        <Route
          path="*"
          element={
            <Navigate
              to={user ? '/home' : '/login'}
              replace
            />
          }
        />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
