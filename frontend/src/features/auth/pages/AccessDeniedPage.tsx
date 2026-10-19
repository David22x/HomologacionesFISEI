
import { Link } from 'react-router-dom';

export default function AccessDeniedPage() {
  return (
    <main className="denied-page">
      <section className="denied-card">
        <p className="eyebrow">ERROR 403</p>
        <h1>Acceso denegado</h1>
        <p>
          No tienes permisos para acceder a esta sección
          del sistema.
        </p>
        <Link className="primary-link" to="/home">
          Volver al inicio
        </Link>
      </section>
    </main>
  );
}
