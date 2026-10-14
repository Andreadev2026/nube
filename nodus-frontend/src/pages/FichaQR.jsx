import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import api from '../api';
import { puedeHacer, ROL_PERSONAL_ASISTENCIAL } from '../permisos';
import { useToast, mensajeDeError } from '../components/Toast';

// Pantalla que abre el QR del equipo: ficha de solo lectura + reporte de falla.
// Es la única pantalla del rol Personal asistencial.

export const CLAVE_ULTIMO_QR = 'nodus_ultimo_qr';

const MAX = { nombreCompleto: 100, legajo: 30, descripcion: 1000 };
const MSJ_DUPLICADO = 'Este equipo ya tiene una falla reportada hoy. Mantenimiento ya fue notificado.';
const FORM_VACIO = { nombreCompleto: '', legajo: '', descripcion: '' };
const NO_ENCONTRADO = 'no_encontrado';

// Con key por equipo, el estado (formulario, aviso) arranca de cero al pasar a otro QR
function FichaQR(props) {
  const { idEquipo } = useParams();
  return <FichaQREquipo key={idEquipo} idEquipo={idEquipo} {...props} />;
}

function FichaQREquipo({ idEquipo, auth, onLogout }) {
  const navigate = useNavigate();
  const toast = useToast();
  const [ficha, setFicha] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [form, setForm] = useState(FORM_VACIO);
  const [errores, setErrores] = useState({});
  const [enviando, setEnviando] = useState(false);
  // 'cargado': ya estaba reportado al abrir la ficha; 'conflicto': otra persona reportó mientras se escribía
  const [aviso, setAviso] = useState(null);

  const rol = auth?.usuario?.rol;
  const esPersonalAsistencial = rol === ROL_PERSONAL_ASISTENCIAL;
  const puedeReportar = puedeHacer(rol, 'reportar_falla');

  useEffect(() => {
    let cancelado = false;
    api.get(`/equipos/${idEquipo}/ficha-qr`)
      .then(r => {
        if (cancelado) return;
        // Para volver a esta ficha si Personal asistencial navega a otra ruta (solo si el equipo existe)
        try { sessionStorage.setItem(CLAVE_ULTIMO_QR, idEquipo); } catch { /* sin storage: no pasa nada */ }
        setFicha(r.data);
        if (r.data.reportadoHoy) setAviso('cargado');
      })
      .catch(err => {
        if (cancelado) return;
        setError(err.response?.status === 404
          ? NO_ENCONTRADO
          : 'No se pudo cargar la ficha del equipo. Intentá de nuevo.');
      })
      .finally(() => { if (!cancelado) setCargando(false); });
    return () => { cancelado = true; };
  }, [idEquipo]);

  const cambiar = (campo, valor) => {
    setForm(f => ({ ...f, [campo]: valor }));
    if (errores[campo]) setErrores(e => { const n = { ...e }; delete n[campo]; return n; });
  };

  const validar = () => {
    const e = {};
    const etiquetas = { nombreCompleto: 'el nombre completo', legajo: 'el legajo o matrícula', descripcion: 'la descripción del problema' };
    for (const campo of Object.keys(MAX)) {
      const v = form[campo].trim();
      if (!v) e[campo] = `Falta completar ${etiquetas[campo]}`;
      else if (v.length > MAX[campo]) e[campo] = `Máximo ${MAX[campo]} caracteres`;
    }
    setErrores(e);
    return Object.keys(e).length === 0;
  };

  const enviar = async (e) => {
    e.preventDefault();
    if (enviando || aviso || !validar()) return;
    setEnviando(true);
    try {
      await api.post(`/equipos/${idEquipo}/reportes-falla`, {
        nombreCompleto: form.nombreCompleto.trim(),
        legajo: form.legajo.trim(),
        descripcion: form.descripcion.trim()
      });
      toast.success('Reporte enviado', 'Mantenimiento recibió tu aviso');
      setForm(FORM_VACIO);
      setAviso('cargado');
    } catch (err) {
      if (err.response?.status === 409) {
        // Otra persona reportó justo antes: se conserva lo escrito hasta cerrar el aviso
        toast.warning('Falla ya reportada', MSJ_DUPLICADO);
        setAviso('conflicto');
      } else {
        toast.error('No se pudo enviar el reporte', mensajeDeError(err, 'Error de conexión con el servidor.'));
      }
    } finally {
      setEnviando(false);
    }
  };

  // Al cerrar el aviso de conflicto se descarta lo escrito; el envío sigue bloqueado
  const cerrarAviso = () => {
    setForm(FORM_VACIO);
    setAviso('cargado');
  };

  const barraSuperior = (
    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginBottom: 12 }}>
      {esPersonalAsistencial && (
        <button onClick={onLogout} style={{ ...btnGhost, width: 'auto', padding: '8px 14px' }}>Cerrar sesión</button>
      )}
    </div>
  );

  if (cargando) {
    return (
      <div style={contenedor}>
        {barraSuperior}
        <div style={{ textAlign: 'center', padding: 60, color: '#9E9CBB' }}>Cargando ficha del equipo...</div>
      </div>
    );
  }

  if (error === NO_ENCONTRADO) {
    return (
      <div style={contenedor}>
        {barraSuperior}
        <div style={{ ...cardStyle, textAlign: 'center', marginTop: 24, padding: '28px 20px' }}>
          <div style={{ fontSize: 17, fontWeight: 700, color: '#712B13', marginBottom: 8 }}>Equipo no encontrado</div>
          <div style={{ fontSize: 13.5, color: '#6B7C8C', lineHeight: 1.5, marginBottom: 20 }}>
            El código QR no corresponde a ningún equipo registrado. Verificá que sea el QR pegado en el equipo.
          </div>
          {/* state.inicio: Personal asistencial ve su pantalla de inicio y no vuelve al último QR */}
          <button onClick={() => navigate('/', { replace: true, state: { inicio: true } })} style={btnSave}>
            Volver al inicio
          </button>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div style={contenedor}>
        {barraSuperior}
        <div style={{ textAlign: 'center', marginTop: 40 }}>
          <div style={{ fontSize: 15, fontWeight: 600, color: '#712B13', marginBottom: 8 }}>{error}</div>
        </div>
      </div>
    );
  }

  const ce = chipEstado(ficha.colorEstado);

  return (
    <div style={contenedor}>
      {barraSuperior}

      {/* Encabezado */}
      <div style={{ textAlign: 'center', marginBottom: 20 }}>
        <div style={{
          width: 60, height: 60, borderRadius: 16, margin: '0 auto 12px',
          background: 'linear-gradient(135deg, #EEF3FB 0%, #DCE7F5 100%)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 28
        }}>+</div>
        <h2 style={{ fontSize: 18, fontWeight: 700, color: '#1a1a1a', margin: 0, overflowWrap: 'anywhere' }}>{ficha.nombre}</h2>
        <div style={{ fontSize: 13, color: '#9E9CBB', fontFamily: 'monospace', marginTop: 2 }}>S/N {ficha.nroSerie}</div>
        <span style={{
          display: 'inline-block', marginTop: 10, padding: '5px 14px', borderRadius: 999,
          background: ce.bg, color: ce.fg, fontSize: 12, fontWeight: 600
        }}>{ficha.estado || '-'}</span>
      </div>

      <div style={cardStyle}>
        <div style={tituloCard}>Identificación</div>
        <FilaDato label="Marca" valor={ficha.marca} />
        <FilaDato label="Tipo de activo" valor={ficha.tipoActivo} />
        <FilaDato label="Nº de serie" valor={ficha.nroSerie} />
        <FilaDato label="Ubicación" valor={ficha.ubicacion} />
        <FilaDato label="Origen" valor={ficha.origen} />
        <FilaDato label="Fecha de alta" valor={fechaDMY(ficha.fechaAlta)} />
        <FilaDato label="Alertas registradas" valor={ficha.alertas?.length ? ficha.alertas.join(', ') : 'Ninguna'} ultima />
      </div>

      <div style={cardStyle}>
        <div style={tituloCard}>Componentes registrados</div>
        {ficha.componentes?.length > 0 ? ficha.componentes.map((c, idx) => (
          <div key={idx} style={{
            padding: '8px 0', borderBottom: idx < ficha.componentes.length - 1 ? '1px solid #F1F4F7' : 'none',
            fontSize: 13, color: '#2D3748'
          }}>{c}</div>
        )) : (
          <div style={{ fontSize: 13, color: '#9E9CBB' }}>Sin componentes registrados</div>
        )}
      </div>

      {puedeReportar && (
        <div style={cardStyle}>
          <div style={{ ...tituloCard, marginBottom: 2, letterSpacing: 0.4 }}>REPORTAR FALLA</div>
          <div style={{ fontSize: 12, color: '#9E9CBB', marginBottom: 14 }}>Todos los campos son obligatorios</div>

          {aviso && (
            <div role="alert" style={{
              background: '#FAEEDA', border: '1px solid #F2D9AE', borderLeft: '4px solid #A37715',
              borderRadius: 10, padding: '12px 14px', fontSize: 13.5, color: '#633806', lineHeight: 1.45,
              marginBottom: aviso === 'conflicto' ? 12 : 0
            }}>
              <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start' }}>
                <span aria-hidden="true" style={{ fontSize: 16, lineHeight: 1.2 }}>⚠</span>
                <div style={{ flex: 1, fontWeight: 600 }}>{MSJ_DUPLICADO}</div>
              </div>
              {aviso === 'conflicto' && (
                <button type="button" onClick={cerrarAviso} style={{ ...btnGhost, marginTop: 10, padding: '9px 14px' }}>
                  Cerrar aviso
                </button>
              )}
            </div>
          )}

          {/* En conflicto se muestra lo escrito (deshabilitado) hasta cerrar el aviso */}
          {aviso !== 'cargado' && (
            <form onSubmit={enviar} noValidate>
              <Campo label="Nombre completo" error={errores.nombreCompleto}>
                <input
                  type="text" value={form.nombreCompleto} maxLength={MAX.nombreCompleto}
                  onChange={e => cambiar('nombreCompleto', e.target.value)}
                  disabled={!!aviso} autoComplete="name" style={inputStyle(errores.nombreCompleto)}
                />
              </Campo>
              <Campo label="Legajo / matrícula" error={errores.legajo}>
                <input
                  type="text" value={form.legajo} maxLength={MAX.legajo}
                  onChange={e => cambiar('legajo', e.target.value)}
                  disabled={!!aviso} style={inputStyle(errores.legajo)}
                />
              </Campo>
              <Campo label="Descripción del problema" error={errores.descripcion}
                ayuda={`${form.descripcion.length}/${MAX.descripcion}`}>
                <textarea
                  value={form.descripcion} maxLength={MAX.descripcion} rows={4}
                  onChange={e => cambiar('descripcion', e.target.value)}
                  disabled={!!aviso} style={{ ...inputStyle(errores.descripcion), resize: 'vertical', minHeight: 96 }}
                />
              </Campo>
              <button type="submit" disabled={enviando || !!aviso}
                style={{ ...btnSave, opacity: enviando || aviso ? 0.6 : 1, cursor: enviando || aviso ? 'not-allowed' : 'pointer' }}>
                {enviando ? 'Enviando...' : 'Enviar reporte'}
              </button>
            </form>
          )}
        </div>
      )}

      {!esPersonalAsistencial && puedeHacer(rol, 'ver_ficha_completa') && (
        <button onClick={() => navigate(`/ficha/${ficha.idEquipo}`)} style={{ ...btnGhost, marginTop: 6 }}>
          Ver ficha completa
        </button>
      )}
    </div>
  );
}

function FilaDato({ label, valor, ultima }) {
  return (
    <div style={{
      display: 'flex', justifyContent: 'space-between', gap: 12, padding: '7px 0',
      borderBottom: ultima ? 'none' : '1px solid #F8FAFC', fontSize: 13
    }}>
      <span style={{ color: '#9E9CBB', flexShrink: 0 }}>{label}</span>
      <span style={{ color: '#1a1a1a', fontWeight: 500, textAlign: 'right', overflowWrap: 'anywhere' }}>{valor || '-'}</span>
    </div>
  );
}

function Campo({ label, error, ayuda, children }) {
  return (
    <label style={{ display: 'block', marginBottom: 12 }}>
      <span style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, fontWeight: 600, color: '#4A5868', marginBottom: 5 }}>
        <span>{label} <span style={{ color: '#D14343' }}>*</span></span>
        {ayuda && <span style={{ fontWeight: 400, color: '#9E9CBB' }}>{ayuda}</span>}
      </span>
      {children}
      {error && <span style={{ display: 'block', fontSize: 12, color: '#A32D2D', marginTop: 4 }}>⚠ {error}</span>}
    </label>
  );
}

const chipEstado = (color) => {
  const map = {
    verde: { bg: '#E1F5EE', fg: '#0F6E56' },
    amarillo: { bg: '#FAEEDA', fg: '#633806' },
    gris: { bg: '#F1EFE8', fg: '#444441' },
    rojo: { bg: '#FAECE7', fg: '#712B13' },
    azul: { bg: '#E6F1FB', fg: '#0C447C' }
  };
  return map[color] || map.gris;
};

// "2026-09-24T00:00:00Z" → "24/09/2026"
const fechaDMY = (iso) => iso ? iso.split('T')[0].split('-').reverse().join('/') : null;

const contenedor = { maxWidth: 560, margin: '0 auto', padding: '16px 16px 60px', boxSizing: 'border-box', width: '100%' };
const cardStyle = {
  background: 'white', border: '1px solid #EEF1F4', borderRadius: 14,
  padding: '16px 18px', marginBottom: 14, boxShadow: '0 1px 3px rgba(0,0,0,0.03)'
};
const tituloCard = { fontSize: 13, fontWeight: 700, color: '#1a1a1a', marginBottom: 10 };
// 16px de fuente para que iOS no haga zoom al enfocar
const inputStyle = (error) => ({
  width: '100%', boxSizing: 'border-box', padding: '10px 12px', borderRadius: 10, fontSize: 16,
  border: `1px solid ${error ? '#E8A5A5' : '#E5EDF3'}`, background: 'white', color: '#1a1a1a',
  fontFamily: 'inherit', outline: 'none'
});
const btnGhost = {
  background: 'white', color: '#6B7C8C', border: '1px solid #E5EDF3',
  padding: '12px 18px', borderRadius: 10, fontSize: 13, fontWeight: 600, cursor: 'pointer', width: '100%'
};
const btnSave = {
  background: 'linear-gradient(135deg, #2C7DA0 0%, #4A9BB8 100%)',
  color: 'white', border: 'none', padding: '12px 18px', borderRadius: 10,
  fontSize: 13, fontWeight: 600, cursor: 'pointer', width: '100%'
};

export default FichaQR;
