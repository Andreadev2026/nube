// URL que se codifica en el QR de un equipo. Todo QR que se genere o imprima debe salir de acá.
// VITE_PUBLIC_BASE_URL es la dirección con la que los celulares llegan al sistema (Vite la incorpora
// al compilar). Si no está definida se usa el origen actual, que no sirve si se abrió desde localhost.

export const AVISO_QR_LOCALHOST =
  'Este QR apunta a localhost y no funcionará desde otros dispositivos. Configurá la dirección pública del sistema.';

export function baseUrlPublica() {
  const configurada = (import.meta.env.VITE_PUBLIC_BASE_URL || '').trim().replace(/\/+$/, '');
  return configurada || window.location.origin;
}

export function urlQrEquipo(idEquipo) {
  return `${baseUrlPublica()}/equipos/${idEquipo}/qr`;
}

/** true si la URL solo funciona en esta misma máquina. */
export function apuntaALocalhost(url) {
  const texto = (url || '').toLowerCase();
  return texto.includes('localhost') || texto.includes('127.0.0.1');
}

/** Imagen del QR (servicio externo) para la URL dada. */
export function imagenQr(url, tamanio = 260) {
  return `https://api.qrserver.com/v1/create-qr-code/?size=${tamanio}x${tamanio}&data=${encodeURIComponent(url)}`;
}
