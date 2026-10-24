import { useState, useEffect, useMemo, useCallback } from 'react';
import api from '../api';
import { useToast, mensajeDeError } from '../components/Toast';
import ConfirmarDialogo from '../components/ConfirmarDialogo';

// Reportes de falla enviados desde la ficha QR. Mantenimiento los revisa y puede descartarlos.
// Solo Administrador, Jefe patrimonio y Jefe servicio (ver permisos.js y el backend).

const ESTADOS = ['Pendiente', 'Convertido en OT', 'Descartado'];

// Minúsculas y sin tildes, para que "jose" encuentre "José"
const normalizar = (s) => String(s ?? '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');

function ReportesFalla() {
  const toast = useToast();
  const [reportes, setReportes] = useState([]);
  const [filtroEstado, setFiltroEstado] = useState('Pendiente');
  const [busqueda, setBusqueda] = useState('');
  const [cargando, setCargando] = useState(true);
  const [procesando, setProcesando] = useState(null);
  // Reporte a descartar (abre el diálogo de confirmación)
  const [aDescartar, setADescartar] = useState(null);
  // Se incrementa para volver a pedir la lista
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let cancelado = false;
    api.get('/reportes-falla', { params: filtroEstado ? { estado: filtroEstado } : {} })
      .then(r => { if (!cancelado) setReportes(r.data); })
      .catch(err => {
        if (!cancelado) toast.error('No se pudieron cargar los reportes', mensajeDeError(err, 'Error de conexión con el servidor.'));
      })
      .finally(() => { if (!cancelado) setCargando(false); });
    return () => { cancelado = true; };
  }, [filtroEstado, version, toast]);

  const cargar = () => { setCargando(true); setVersion(v => v + 1); };
  const cambiarFiltro = (estado) => { setCargando(true); setFiltroEstado(estado); };

  // Filtra en el momento sobre equipo (nombre y serie), quién reportó (nombre y legajo), servicio y descripción
  const visibles = useMemo(() => {
    const q = normalizar(busqueda.trim());
    if (!q) return reportes;
    return reportes.filter(r =>
      [r.equipo, r.nroSerie, r.nombreCompleto, r.legajo, r.servicio, r.descripcion].some(c => normalizar(c).includes(q))
    );
  }, [reportes, busqueda]);

  const cancelarDescarte = useCallback(() => setADescartar(null), []);

  const confirmarDescarte = async () => {
    const rep = aDescartar;
    setProcesando(rep.idReporteFalla);
    try {
      await api.put(`/reportes-falla/${rep.idReporteFalla}/descartar`);
      toast.info('Reporte descartado', 'El equipo se puede volver a reportar.');
      setADescartar(null);
      cargar();
    } catch (err) {
      toast.error('No se pudo descartar', mensajeDeError(err, 'Error de conexión con el servidor.'));
    } finally {
      setProcesando(null);
    }
  };

  const chipEstado = (estado) => {
    const map = {
      'Pendiente': { bg: '#FAEEDA', fg: '#633806' },
      'Convertido en OT': { bg: '#E1F5EE', fg: '#0F6E56' },
      'Descartado': { bg: '#F1EFE8', fg: '#444441' }
    };
    const c = map[estado] || map.Descartado;
    return { background: c.bg, color: c.fg, padding: '3px 10px', borderRadius: 999, fontSize: 11.5, fontWeight: 600, whiteSpace: 'nowrap' };
  };

  return (
    <>
      <header className="topbar">
        <h2>REGISTROS DE FALLA</h2>
        <span className="badge" style={{ background: 'rgba(245,166,35,0.15)', color: '#A37715', borderColor: 'rgba(245,166,35,0.3)' }}>QR</span>
        <span className="desc">Fallas reportadas desde la ficha QR de los equipos</span>
      </header>

      <div className="page-content">
        <div className="form-card rf-filtros">
          <div className="rf-busqueda">
            <span className="rf-lupa">🔍</span>
            <input
              type="text" value={busqueda} onChange={e => setBusqueda(e.target.value)}
              placeholder="Buscar por equipo, quién reportó, servicio o descripción..."
              aria-label="Buscar reportes"
            />
            {busqueda && (
              <button type="button" className="rf-limpiar" onClick={() => setBusqueda('')}
                title="Limpiar búsqueda" aria-label="Limpiar búsqueda">×</button>
            )}
          </div>
          <div className="rf-acciones">
            <select className="rf-select" value={filtroEstado} onChange={e => cambiarFiltro(e.target.value)} aria-label="Filtrar por estado">
              <option value="">Todos los estados</option>
              {ESTADOS.map(e => <option key={e} value={e}>{e}</option>)}
            </select>
            <button type="button" className="btn-ghost rf-actualizar" onClick={cargar} disabled={cargando}
              title="Actualizar" aria-label="Actualizar">↻</button>
          </div>
        </div>

        <div className="table-card">
          <div className="table-header">
            <span>{visibles.length} {visibles.length === 1 ? 'reporte' : 'reportes'}</span>
          </div>
          <table>
            <thead>
              <tr>
                <th className="rf-nro">N°</th>
                <th>Fecha</th>
                <th>Equipo</th>
                <th>Reportó</th>
                <th>Servicio</th>
                <th>Descripción</th>
                <th>Estado</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {visibles.map((r, i) => (
                <tr key={r.idReporteFalla}>
                  <td className="rf-nro">{i + 1}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    {new Date(r.fechaReporte).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })}
                  </td>
                  <td><strong>{r.equipo}</strong><div style={{ fontSize: 11.5, color: '#9E9CBB', fontFamily: 'monospace' }}>S/N {r.nroSerie}</div></td>
                  <td>
                    {r.nombreCompleto}
                    <div style={{ fontSize: 11.5, color: '#9E9CBB' }}>Legajo {r.legajo} · usuario {r.usuario}</div>
                  </td>
                  <td>{r.servicio || <span style={{ color: '#9E9CBB' }}>—</span>}</td>
                  <td className="rf-descripcion">{r.descripcion}</td>
                  <td>
                    <span style={chipEstado(r.estado)}>{r.estado}</span>
                    {r.idOT && <div style={{ fontSize: 11.5, color: '#9E9CBB', marginTop: 4 }}>OT #{r.idOT}</div>}
                  </td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    {r.estado === 'Pendiente' ? (
                      <button className="btn-ghost" style={{ fontSize: 12, padding: '6px 10px' }}
                        disabled={procesando === r.idReporteFalla} onClick={() => setADescartar(r)}>
                        Descartar
                      </button>
                    ) : '—'}
                  </td>
                </tr>
              ))}
              {!cargando && visibles.length === 0 && (
                <tr><td colSpan="8" className="empty">
                  {busqueda.trim() ? 'No se encontraron reportes para tu búsqueda.'
                    : filtroEstado ? `No hay reportes en estado "${filtroEstado}".` : 'No hay reportes de falla.'}
                </td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {aDescartar && (
        <ConfirmarDialogo
          titulo="Descartar reporte"
          mensaje={<>
            ¿Descartar el reporte de <strong>{aDescartar.nombreCompleto}</strong> sobre <strong>{aDescartar.equipo}</strong>?
            <br />El equipo se podrá volver a reportar hoy.
          </>}
          textoConfirmar="Descartar"
          destructivo
          procesando={procesando === aDescartar.idReporteFalla}
          onConfirmar={confirmarDescarte}
          onCancelar={cancelarDescarte}
        />
      )}
    </>
  );
}

export default ReportesFalla;
