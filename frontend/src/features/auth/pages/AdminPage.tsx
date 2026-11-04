
import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  userManagementService,
  type ManagedUser,
  type UpdateManagedUser,
} from '../services/authService';

export default function AdminPage({ currentUserId }: { currentUserId: string }) {
  const [users, setUsers] = useState<ManagedUser[]>([]);
  const [editingId, setEditingId] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  async function loadUsers() {
    setError('');
    try { setUsers(await userManagementService.listar()); }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'No se pudieron cargar los usuarios.'); }
    finally { setLoading(false); }
  }

  useEffect(() => { void loadUsers(); }, []);

  function beginEdit(user: ManagedUser) {
    setEditingId(user.id);
    setNotice(''); setError('');
    window.setTimeout(() => {
      const form = document.querySelector<HTMLFormElement>('#managed-user-form');
      if (!form) return;
      (form.elements.namedItem('email') as HTMLInputElement).value = user.email;
      (form.elements.namedItem('nombreCompleto') as HTMLInputElement).value = user.nombreCompleto;
      (form.elements.namedItem('rol') as HTMLSelectElement).value = user.roles[0] ?? 'Profesor';
      (form.elements.namedItem('activo') as HTMLInputElement).checked = user.activo;
    }, 0);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const values = new FormData(form);
    const email = String(values.get('email') ?? '').trim();
    const nombreCompleto = String(values.get('nombreCompleto') ?? '').trim();
    const rol = String(values.get('rol')) as UpdateManagedUser['rol'];
    setBusy(true); setError(''); setNotice('');
    try {
      if (editingId) {
        await userManagementService.actualizar(editingId, {
          email, nombreCompleto, rol,
          activo: (form.elements.namedItem('activo') as HTMLInputElement).checked,
        });
        setNotice('Usuario actualizado.');
      } else {
        await userManagementService.crear({ email, nombreCompleto, rol, password: String(values.get('password') ?? '') });
        setNotice('Usuario creado.');
      }
      setEditingId(''); form.reset(); await loadUsers();
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'No se pudo guardar el usuario.'); }
    finally { setBusy(false); }
  }

  return <main className="catalog-page">
    <header className="catalog-header"><div><p className="catalog-eyebrow">HOMOLOGACIONES FISEI · ADMINISTRACIÓN</p><h1>Usuarios y roles</h1><p>Crea cuentas, asigna roles y cambia el estado de acceso.</p></div><Link to="/home" className="catalog-back-link">Volver al inicio</Link></header>
    {error && <p className="catalog-error" role="alert">{error}</p>}
    {notice && <p className="maintenance-notice" role="status">{notice}</p>}
    <section className="students-panel">
      <h2>{editingId ? 'Editar usuario' : 'Crear usuario'}</h2>
      <form id="managed-user-form" className="student-form" onSubmit={submit} key={editingId || 'new'}>
        <label>Correo electrónico<input name="email" type="email" required maxLength={256} /></label>
        <label>Nombre completo<input name="nombreCompleto" required minLength={2} maxLength={200} /></label>
        {!editingId && <label>Contraseña inicial<input name="password" type="password" required minLength={6} autoComplete="new-password" /><small>Debe tener al menos 6 caracteres y un número.</small></label>}
        <label>Rol<select name="rol" defaultValue="Profesor"><option value="Profesor">Profesor</option><option value="Administrador">Administrador</option></select></label>
        {editingId && <label className="check-field"><input name="activo" type="checkbox" defaultChecked /> Cuenta activa</label>}
        <div className="student-form-actions"><button className="students-primary" disabled={busy}>{busy ? 'Guardando…' : editingId ? 'Guardar cambios' : 'Crear cuenta'}</button>{editingId && <button type="button" className="students-secondary" onClick={() => setEditingId('')}>Cancelar</button>}</div>
      </form>
    </section>
    <section className="students-panel">
      <div className="students-panel-heading"><div><h2>Usuarios registrados</h2><p>{users.length} cuentas</p></div></div>
      {loading ? <p role="status">Cargando usuarios…</p> : users.length === 0 ? <p>No hay usuarios registrados.</p> : <div className="student-table-wrap"><table className="student-table">
        <thead><tr><th>Nombre</th><th>Correo</th><th>Rol</th><th>Estado</th><th>Acciones</th></tr></thead>
        <tbody>{users.map(user => <tr key={user.id}><td>{user.nombreCompleto}</td><td>{user.email}</td><td>{user.roles.join(', ')}</td><td>{user.activo ? 'Activo' : 'Inactivo'}</td><td><button className="students-secondary" type="button" onClick={() => beginEdit(user)} disabled={busy}>Editar</button>{user.id === currentUserId && <small> Tu cuenta</small>}</td></tr>)}</tbody>
      </table></div>}
    </section>
  </main>;
}
