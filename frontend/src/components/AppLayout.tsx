import { useEffect, useState, type ReactNode } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import type { AuthUser } from '../features/auth/services/authService';
import './AppLayout.css';
import './forms.css';

interface AppLayoutProps {
  user: AuthUser;
  onLogout: () => void;
}

interface NavItem {
  to: string;
  label: string;
  icon: ReactNode;
  end?: boolean;
}

interface NavSection {
  title?: string;
  items: NavItem[];
}

function Icon({ children }: { children: ReactNode }) {
  return (
    <svg
      className="nav-icon"
      viewBox="0 0 24 24"
      width="20"
      height="20"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {children}
    </svg>
  );
}

const icons = {
  home: <Icon><path d="M3 11l9-8 9 8" /><path d="M5 10v10h14V10" /></Icon>,
  catalog: <Icon><path d="M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2z" /><path d="M4 21V5" /></Icon>,
  students: <Icon><circle cx="9" cy="8" r="3.5" /><path d="M2.5 20a6.5 6.5 0 0 1 13 0" /><path d="M16 4.5a3.5 3.5 0 0 1 0 7" /><path d="M18 14a6 6 0 0 1 3.5 6" /></Icon>,
  folder: <Icon><path d="M3 6a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" /></Icon>,
  upload: <Icon><path d="M12 16V4" /><path d="M7 9l5-5 5 5" /><path d="M4 20h16" /></Icon>,
  settings: <Icon><circle cx="12" cy="12" r="3" /><path d="M19 12a7 7 0 0 0-.1-1.2l2-1.5-2-3.4-2.4 1a7 7 0 0 0-2-1.2L14 3h-4l-.5 2.7a7 7 0 0 0-2 1.2l-2.4-1-2 3.4 2 1.5A7 7 0 0 0 5 12c0 .4 0 .8.1 1.2l-2 1.5 2 3.4 2.4-1a7 7 0 0 0 2 1.2L10 21h4l.5-2.7a7 7 0 0 0 2-1.2l2.4 1 2-3.4-2-1.5c.1-.4.1-.8.1-1.2z" /></Icon>,
  users: <Icon><circle cx="12" cy="8" r="4" /><path d="M4 21a8 8 0 0 1 16 0" /></Icon>,
};

function buildSections(user: AuthUser): NavSection[] {
  const isAdmin = user.roles.includes('Administrador');
  const isTeacher = user.roles.includes('Profesor');
  const canManageStudents = isAdmin || isTeacher;

  const sections: NavSection[] = [
    {
      items: [
        { to: '/home', label: 'Inicio', icon: icons.home },
        { to: '/catalogo', label: 'Catálogo académico', icon: icons.catalog, end: true },
      ],
    },
  ];

  if (canManageStudents) {
    sections.push({
      title: 'Estudiantes',
      items: [
        { to: '/estudiantes', label: 'Estudiantes', icon: icons.students, end: true },
        { to: '/expedientes', label: 'Expedientes', icon: icons.folder },
        { to: '/estudiantes/importar', label: 'Carga de historiales', icon: icons.upload },
      ],
    });
  }

  if (isAdmin) {
    sections.push({
      title: 'Administración',
      items: [
        { to: '/catalogo/mantenimiento', label: 'Mantenimiento del catálogo', icon: icons.settings },
        { to: '/admin', label: 'Usuarios y roles', icon: icons.users },
      ],
    });
  }

  return sections;
}

export default function AppLayout({ user, onLogout }: AppLayoutProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const sections = buildSections(user);

  useEffect(() => {
    function closeOnWideScreen() {
      if (window.innerWidth > 900) setMenuOpen(false);
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') setMenuOpen(false);
    }
    window.addEventListener('resize', closeOnWideScreen);
    window.addEventListener('keydown', closeOnEscape);
    return () => {
      window.removeEventListener('resize', closeOnWideScreen);
      window.removeEventListener('keydown', closeOnEscape);
    };
  }, []);

  return (
    <div className="app-shell">
      <header className="app-topbar">
        <button
          type="button"
          className="app-menu-toggle"
          aria-label={menuOpen ? 'Cerrar menú' : 'Abrir menú'}
          aria-expanded={menuOpen}
          aria-controls="app-sidebar"
          onClick={() => setMenuOpen((open) => !open)}
        >
          <span />
          <span />
          <span />
        </button>
        <span className="app-topbar-title">Homologaciones FISEI</span>
      </header>

      {menuOpen && (
        <div
          className="app-backdrop"
          onClick={() => setMenuOpen(false)}
          aria-hidden="true"
        />
      )}

      <aside
        id="app-sidebar"
        className={`app-sidebar${menuOpen ? ' is-open' : ''}`}
      >
        <div className="sidebar-brand">
          <span className="brand-mark">H</span>
          <span>Homologaciones FISEI</span>
        </div>

        <nav className="sidebar-nav" aria-label="Menú principal">
          {sections.map((section, index) => (
            <div className="sidebar-section" key={section.title ?? `section-${index}`}>
              {section.title && (
                <p className="sidebar-section-title">{section.title}</p>
              )}
              {section.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  onClick={() => setMenuOpen(false)}
                  className={({ isActive }) =>
                    `sidebar-link${isActive ? ' is-active' : ''}`
                  }
                >
                  {item.icon}
                  <span>{item.label}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <div className="sidebar-user">
          <div className="sidebar-user-info">
            <span className="sidebar-avatar" aria-hidden="true">
              {user.nombreCompleto.trim().charAt(0).toUpperCase() || '?'}
            </span>
            <div>
              <strong>{user.nombreCompleto}</strong>
              <small>{user.roles.join(', ')}</small>
            </div>
          </div>
          <button type="button" className="sidebar-logout" onClick={onLogout}>
            Cerrar sesión
          </button>
        </div>
      </aside>

      <div className="app-content" key={user.id}>
        <Outlet />
      </div>
    </div>
  );
}
