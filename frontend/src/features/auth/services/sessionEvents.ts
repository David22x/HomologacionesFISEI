const SESSION_EXPIRED_EVENT = 'homologaciones:session-expired';

/** Avisa a la aplicación que el servidor rechazó el token (401). */
export function notifySessionExpired(): void {
  window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
}

/** Escucha el aviso de sesión expirada. Devuelve la función para cancelar la suscripción. */
export function onSessionExpired(callback: () => void): () => void {
  window.addEventListener(SESSION_EXPIRED_EVENT, callback);
  return () => window.removeEventListener(SESSION_EXPIRED_EVENT, callback);
}
