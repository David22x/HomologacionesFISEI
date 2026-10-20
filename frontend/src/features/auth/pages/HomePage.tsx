
import { Link } from 'react-router-dom';
import type { AuthUser } from '../services/authService';

interface HomePageProps {
  user: AuthUser;
  onLogout: () => void;
}

export default function HomePage({
  user,
  onLogout,
}: HomePageProps) {
  const isAdmin = user.roles.includes('Administrador');
  const isTeacher = user.roles.includes('Profesor');

  return (
    <main className="home-page">
      <header className="home-header">
        <div className="brand">
          <span className="brand-mark">H</span>
          <span>Homologaciones FISEI</span>
        </div>

        <button className="logout-button" onClick={onLogout}>
          Cerrar sesión
        </button>
      </header>

      <nav className="role-menu" aria-label="Menú principal">
        <Link to="/home">Inicio</Link>
        <Link to="/catalogo">Catálogo académico</Link>
        {isAdmin && (
          <Link to="/admin">Administración</Link>
        )}

        {isTeacher && (
          <Link to="/docente">Área docente</Link>
        )}
      </nav>

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
      </section>
    </main>
  );
}
