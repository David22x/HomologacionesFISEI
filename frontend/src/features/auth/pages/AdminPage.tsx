
import { Link } from 'react-router-dom';

export default function AdminPage() {
  return (
    <main className="welcome-card">
      <h1>Administración</h1>
      <p>Sección disponible para el rol Administrador.</p>
      <Link to="/home">Volver al inicio</Link>
    </main>
  );
}
