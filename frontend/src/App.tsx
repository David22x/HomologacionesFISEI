
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

import ProtectedRoute from './components/ProtectedRoute';
import HomePage from './features/auth/pages/HomePage';
import AccessDeniedPage from './features/auth/pages/AccessDeniedPage';
import AdminPage from './features/auth/pages/AdminPage';
import TeacherPage from './features/auth/pages/TeacherPage';
import './App.css';
import CatalogPage from './features/catalog/pages/CatalogPage';
import CatalogMaintenancePage from './features/catalog/pages/CatalogMaintenancePage';
import TemasPage from './features/catalog/pages/TemasPage';

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
          path="/home"
          element={
            <ProtectedRoute user={user}>
              {user && (
                <HomePage user={user} onLogout={handleLogout} />
              )}
            </ProtectedRoute>
          }
        />

        <Route
  path="/catalogo"
  element={
    <ProtectedRoute
      user={user}
      allowedRoles={['Administrador', 'Profesor']}
    >
      <CatalogPage />
    </ProtectedRoute>
  }
/>

        <Route
          path="/admin"
          element={
            <ProtectedRoute
              user={user}
              allowedRoles={['Administrador']}
            >
              <AdminPage />
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

        <Route
          path="/docente"
          element={
            <ProtectedRoute
              user={user}
              allowedRoles={['Profesor']}
            >
              <TeacherPage />
            </ProtectedRoute>
          }
        />

        <Route
          path="/acceso-denegado"
          element={
            <ProtectedRoute user={user}>
              <AccessDeniedPage />
            </ProtectedRoute>
          }
        />

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
