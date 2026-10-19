
import { Link } from 'react-router-dom';

export default function TeacherPage() {
  return (
    <main className="welcome-card">
      <h1>Área docente</h1>
      <p>Sección disponible para el rol Profesor.</p>
      <Link to="/home">Volver al inicio</Link>
    </main>
  );
}
