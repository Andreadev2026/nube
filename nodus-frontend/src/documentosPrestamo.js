// Documentos de un préstamo (remito de salida y de devolución, cada uno con su escaneo firmado):
// URLs, formatos y estado, compartidos entre las pantallas de Préstamos y Remitos.
// Los modales están en components/DocumentosPrestamo.jsx.

import { API_URL } from './api';

const conDescarga = (url, descargar) => `${url}${descargar ? '?descargar=true' : ''}`;

// ruta: 'remito' | 'remito-devolucion' (PDFs generados por el sistema)
export const urlDocumento = (idPrestamo, ruta, descargar = false) =>
  conDescarga(`${API_URL}/api/prestamos/${idPrestamo}/${ruta}`, descargar);
export const urlRemito = (idPrestamo, descargar = false) => urlDocumento(idPrestamo, 'remito', descargar);

// tipo: 'salida' | 'devolucion' (escaneos firmados subidos)
export const urlFirmado = (idPrestamo, tipo, descargar = false) =>
  conDescarga(`${API_URL}/api/remitos/${idPrestamo}/firma-${tipo}`, descargar);

// "2026-09-24T00:00:00Z" → "24/09/2026"
export const fechaDMY = (iso) => iso ? iso.split('T')[0].split('-').reverse().join('/') : '—';
// "2026-09-23T04:12:00Z" → "23/09/26 01:12" en hora local
export const fechaHora = (iso) => iso ? new Date(iso).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' }) : '—';

/** true si algún remito del préstamo (salida, o devolución si existe) todavía no tiene su escaneo firmado. */
export const faltaFirma = (p) => !p.tieneFirmaSalida || (!!p.numeroRemitoDevolucion && !p.tieneFirmaDevolucion);
