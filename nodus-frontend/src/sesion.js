// Sesión guardada en el navegador y destino de vuelta después del login.
// Se guarda en localStorage (no en sessionStorage) porque el QR escaneado con la cámara del celular
// abre una pestaña nueva: así, con sesión activa, la ficha se abre directo sin pasar por el login.

const CLAVE_SESION = 'nodus_sesion';

// Segundos de la fecha de vencimiento (claim "exp") del JWT, o null si no se puede leer
function vencimientoDelToken(token) {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const { exp } = JSON.parse(atob(payload));
    return typeof exp === 'number' ? exp : null;
  } catch {
    return null;
  }
}

/** Sesión guardada si el token todavía no venció; si venció o está dañada, se borra y devuelve null. */
export function cargarSesion() {
  let datos = null;
  try { datos = JSON.parse(localStorage.getItem(CLAVE_SESION)); } catch { /* sin storage o dañada */ }
  const exp = datos?.token ? vencimientoDelToken(datos.token) : null;
  if (!datos?.usuario || !exp || exp * 1000 <= Date.now()) {
    borrarSesion();
    return null;
  }
  return datos;
}

export function guardarSesion(datos) {
  try { localStorage.setItem(CLAVE_SESION, JSON.stringify({ token: datos.token, usuario: datos.usuario })); } catch { /* sin storage */ }
}

export function borrarSesion() {
  try { localStorage.removeItem(CLAVE_SESION); } catch { /* sin storage */ }
}

/**
 * Devuelve el destino si es una ruta interna de la app; si no, null.
 * Rechaza "//sitio.com" y "/\sitio.com" (el navegador los trata como otro dominio), esquemas
 * (https:, javascript:...), caracteres de control y el propio /login.
 */
export function destinoSeguro(destino) {
  if (typeof destino !== 'string' || destino.length > 500) return null;
  if (!destino.startsWith('/') || destino.startsWith('//') || destino.includes('\\')) return null;
  // eslint-disable-next-line no-control-regex
  if (/[\u0000-\u001F\u007F]/.test(destino)) return null;
  try {
    // Con una base inventada: si el destino lograra cambiar de origen, no es interno
    const url = new URL(destino, 'https://nodus.invalid');
    if (url.origin !== 'https://nodus.invalid') return null;
    if (url.pathname === '/login') return null;
  } catch {
    return null;
  }
  return destino;
}

/** true si el destino es la ficha QR de un equipo (ruta actual o la vieja /ficha/{id}). */
export function esDestinoQR(destino) {
  return /^\/(equipos\/\d+\/qr|ficha\/\d+)\/?(\?|#|$)/.test(destino || '');
}
