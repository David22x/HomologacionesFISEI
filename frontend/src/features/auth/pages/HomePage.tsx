import { Link } from 'react-router-dom';
import type { AuthUser } from '../services/authService';

interface HomePageProps {
  user: AuthUser;
}

interface QuickLink {
  to: string;
  title: string;
  description: string;
}

export default function HomePage({ user }: HomePageProps) {
  const isAdmin = user.roles.includes('Administrador');
  const isTeacher = user.roles.includes('Profesor');

  const links: QuickLink[] = [
    {
      to: '/catalogo',
      title: 'Catálogo académico',
      description: 'Consulta carreras, mallas, niveles, asignaturas y temas.',
    },
  ];

  if (isAdmin || isTeacher) {
    links.push(
      {
        to: '/estudiantes',
        title: 'Estudiantes',
        description: 'Registra y consulta a los estudiantes en proceso de homologación.',
      },
      {
        to: '/expedientes',
        title: 'Expedientes',
        description: 'Revisa los expedientes de homologación y su estado.',
      },
      {
        to: '/estudiantes/importar',
        title: 'Carga de historiales',
        description: 'Importa historiales académicos y revisa las cargas anteriores.',
      },
    );
  }

  if (isAdmin) {
    links.push(
      {
        to: '/catalogo/mantenimiento',
        title: 'Mantenimiento del catálogo',
        description: 'Administra carreras, versiones de malla, niveles y asignaturas.',
      },
      {
        to: '/admin',
        title: 'Usuarios y roles',
        description: 'Crea cuentas, asigna roles y controla el acceso.',
      },
    );
  }

  return (
    <main className="home-page">
      <header className="home-intro">
        <p className="eyebrow">HOMOLOGACIONES FISEI</p>
        <h1>¡Bienvenido/a, {user.nombreCompleto}!</h1>
        <p>Elige una opción para continuar.</p>
      </header>

      <section className="home-cards" aria-label="Accesos rápidos">
        {links.map((link) => (
          <Link className="home-card" to={link.to} key={link.to}>
            <h2>{link.title}</h2>
            <p>{link.description}</p>
          </Link>
        ))}
      </section>
    </main>
  );
}
