import { useEffect, useRef } from 'react';

// Diálogo de confirmación con el estilo de los modales del sistema, en lugar de window.confirm.
// Se cierra con Esc o tocando afuera (salvo mientras se procesa la acción).

function ConfirmarDialogo({
  titulo, mensaje, textoConfirmar = 'Confirmar', textoCancelar = 'Cancelar',
  destructivo = false, procesando = false, onConfirmar, onCancelar
}) {
  const btnCancelarRef = useRef(null);

  useEffect(() => {
    // Foco en Cancelar: un Enter accidental no ejecuta la acción
    btnCancelarRef.current?.focus();
  }, []);

  useEffect(() => {
    const alTeclear = (e) => { if (e.key === 'Escape' && !procesando) onCancelar(); };
    window.addEventListener('keydown', alTeclear);
    return () => window.removeEventListener('keydown', alTeclear);
  }, [procesando, onCancelar]);

  return (
    <div onClick={() => procesando || onCancelar()} className="rwd-modal-overlay" style={overlayStyle}>
      <div
        onClick={e => e.stopPropagation()} className="rwd-modal-box"
        role="alertdialog" aria-modal="true" aria-labelledby="confirmar-titulo" aria-describedby="confirmar-mensaje"
        style={modalStyle}
      >
        <div style={modalHeaderStyle}>
          <div style={{ ...iconoStyle, background: destructivo ? '#FDECEE' : '#E6F1FB', color: destructivo ? '#C53030' : '#2C7DA0' }}>
            {destructivo ? '⚠' : '?'}
          </div>
          <h3 id="confirmar-titulo" style={{ margin: 0, color: '#1a1a1a', fontSize: 17, fontWeight: 700 }}>{titulo}</h3>
        </div>
        <div id="confirmar-mensaje" style={{ padding: '18px 28px 22px', fontSize: 14, color: '#4A5868', lineHeight: 1.5 }}>
          {mensaje}
        </div>
        <div style={modalFooterStyle}>
          <button ref={btnCancelarRef} type="button" onClick={onCancelar} disabled={procesando} style={btnGhost}>
            {textoCancelar}
          </button>
          <button type="button" onClick={onConfirmar} disabled={procesando}
            style={{ ...(destructivo ? btnPeligro : btnSave), opacity: procesando ? 0.7 : 1, cursor: procesando ? 'not-allowed' : 'pointer' }}>
            {procesando ? 'Procesando...' : textoConfirmar}
          </button>
        </div>
      </div>
    </div>
  );
}

const overlayStyle = { position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(20,25,40,0.6)', backdropFilter: 'blur(4px)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: 20 };
const modalStyle = { background: 'white', borderRadius: 16, width: '100%', maxWidth: 440, boxShadow: '0 20px 60px rgba(0,0,0,0.25)' };
const modalHeaderStyle = { padding: '22px 28px 16px', borderBottom: '1px solid #F0F3F7', display: 'flex', alignItems: 'center', gap: 14 };
const modalFooterStyle = { padding: '16px 28px', borderTop: '1px solid #F0F3F7', background: '#FAFBFC', borderBottomLeftRadius: 16, borderBottomRightRadius: 16, display: 'flex', justifyContent: 'flex-end', gap: 10 };
const iconoStyle = { width: 40, height: 40, borderRadius: 10, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18, fontWeight: 700, flexShrink: 0 };
const btnGhost = { background: 'white', color: '#6B7C8C', border: '1px solid #E5EDF3', padding: '10px 18px', borderRadius: 8, fontSize: 13, fontWeight: 600, cursor: 'pointer' };
const btnSave = { background: 'linear-gradient(135deg, #2C7DA0 0%, #4A9BB8 100%)', color: 'white', border: 'none', padding: '10px 20px', borderRadius: 8, fontSize: 13, fontWeight: 600, cursor: 'pointer' };
const btnPeligro = { background: '#C53030', color: 'white', border: 'none', padding: '10px 20px', borderRadius: 8, fontSize: 13, fontWeight: 600, cursor: 'pointer' };

export default ConfirmarDialogo;
