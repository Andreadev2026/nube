// Lógica de vencimientos (préstamos y garantías), compartida entre pantallas y el menú lateral,
// para que los contadores den el mismo número en toda la app.

// "YYYY-MM-DDT00:00:00Z" → ms UTC de ese día calendario
const diaUTC = (iso) => {
  const [y, m, d] = iso.split('T')[0].split('-').map(Number);
  return Date.UTC(y, m - 1, d);
};

// Días calendario desde hoy hasta una fecha "YYYY-MM-DDT00:00:00Z" (0 = hoy, negativo = ya pasó).
// Se compara por fecha, sin hora, para que algo no figure vencido el mismo día que vence.
const diasHasta = (iso) => {
  const hoy = new Date();
  return Math.round((diaUTC(iso) - Date.UTC(hoy.getFullYear(), hoy.getMonth(), hoy.getDate())) / 86400000);
};

// ─── Préstamos ───

export const DIAS_AVISO = 2;

// Evento que dispara la pantalla de Préstamos cuando cambian los datos, para que el menú se actualice
export const EVENTO_PRESTAMOS_ACTUALIZADOS = 'nodus:prestamos-actualizados';

// Días hasta la devolución estimada; null si el préstamo no está activo o no tiene fecha estimada
export const diasParaVencer = (p) => {
  if (p.estado !== 'En préstamo' || p.fechaDevolucionReal || !p.fechaEstimadaDevolucion) return null;
  return diasHasta(p.fechaEstimadaDevolucion);
};

export const estaVencido = (p) => { const d = diasParaVencer(p); return d !== null && d < 0; };
export const estaPorVencer = (p) => { const d = diasParaVencer(p); return d !== null && d >= 0 && d <= DIAS_AVISO; };
export const textoVence = (dias) => dias === 0 ? 'Vence hoy' : dias === 1 ? 'Vence mañana' : `Vence en ${dias} días`;

// Días de atraso de una prórroga pedida con el préstamo ya vencido: desde la fecha que vencía hasta el día
// en que se prorrogó (fechaSolicitud es un timestamp real: se toma el día en hora local). 0 si fue a tiempo.
const atrasoAlProrrogar = (pr) => {
  if (!pr.fechaAnterior) return 0;
  const s = new Date(pr.fechaSolicitud);
  return Math.max(0, Math.round((Date.UTC(s.getFullYear(), s.getMonth(), s.getDate()) - diaUTC(pr.fechaAnterior)) / 86400000));
};

// Prórrogas que se pidieron cuando el préstamo ya estaba vencido
export const prorrogasConVencido = (p) => (p.prorrogas || []).filter(pr => atrasoAlProrrogar(pr) > 0).length;

// Días de atraso de un préstamo: los que acumuló antes de cada prórroga pedida estando vencido, más el atraso
// respecto de la devolución estimada vigente (hasta la devolución real, o hasta hoy si sigue en préstamo).
// 0 = siempre a término; null si no hay fecha estimada.
export const diasAtraso = (p) => {
  if (!p.fechaEstimadaDevolucion) return null;
  const previos = (p.prorrogas || []).reduce((acc, pr) => acc + atrasoAlProrrogar(pr), 0);
  if (p.fechaDevolucionReal)
    return previos + Math.max(0, Math.round((diaUTC(p.fechaDevolucionReal) - diaUTC(p.fechaEstimadaDevolucion)) / 86400000));
  if (p.estado !== 'En préstamo') return null;
  return previos + Math.max(0, -diasHasta(p.fechaEstimadaDevolucion));
};

// ─── Garantías de activos ───

export const DIAS_AVISO_GARANTIA = 15;
export const ID_ESTADO_BAJA = 5; // estado_equipo "Baja patrimonial"

export const EVENTO_EQUIPOS_ACTUALIZADOS = 'nodus:equipos-actualizados';

// Días hasta el vencimiento de garantía; null si no tiene garantía cargada o está dado de baja
export const diasParaVencerGarantia = (e) => {
  if (!e.vencimientoGarantia || e.idEstado === ID_ESTADO_BAJA) return null;
  return diasHasta(e.vencimientoGarantia);
};

export const garantiaVencida = (e) => { const d = diasParaVencerGarantia(e); return d !== null && d < 0; };
export const garantiaPorVencer = (e) => { const d = diasParaVencerGarantia(e); return d !== null && d >= 0 && d <= DIAS_AVISO_GARANTIA; };
