import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, NavLink, Navigate, useLocation } from 'react-router-dom';
import {
  IconStethoscope,
  IconMapPin,
  IconArchive,
  IconClipboardList,
  IconSettings,
  IconFileText,
  IconPackage,
  IconArrowBarDown,
  IconUsers,
  IconLogout,
  IconLayoutDashboard,
  IconTools,
  IconMedicalCross,
  IconShieldLock,
  IconChartBar,
  IconList,
  IconArrowsExchange,
  IconBuildingHospital,
  IconMap2,
  IconMenu2,
  IconX
} from '@tabler/icons-react';
import api from './api';
import { puedeVerModulo } from './permisos';
import Login from './pages/Login';
import Marcas from './pages/Marcas';
import Activos from './pages/Activos';
import Usuarios from './pages/Usuarios';
import Sectores from './pages/Sectores';
import Servicios from './pages/Servicios';
import Ubicaciones from './pages/Ubicaciones';
import Insumos from './pages/Insumos';
import OrdenesTrabajo from './pages/OrdenesTrabajo';
import Intervenciones from './pages/Intervenciones';
import UsosInsumo from './pages/UsosInsumo';
import Alertas from './pages/Alertas';
import BajasPatrimoniales from './pages/BajasPatrimoniales';
import HojasDeVida from './pages/HojasDeVida';
import Bienvenida from './pages/Bienvenida';
import Reportes from './pages/Reportes';
import Prestamos from './pages/Prestamos';
import HospitalesExternos from './pages/HospitalesExternos';
import MapaPrestamos from './pages/MapaPrestamos';
import FichaEquipo from './pages/FichaEquipo';
import './App.css';

/* Sidebar como componente separado para usar useLocation */
function Sidebar({ auth, onLogout, mobileOpen, onNavigate }) {
  const location = useLocation();
  const rol = auth.usuario.rol;
  const iniciales = auth.usuario.nombre?.charAt(0).toUpperCase() || 'U';

  // Mapeo: qué rutas pertenecen a qué módulo
  const moduloDeRuta = {
    '/activos': 'patrimonial',
    '/ubicaciones': 'patrimonial',
    '/bajaspatrimoniales': 'patrimonial',
    '/ordenestrabajo': 'mantenimiento',
    '/intervenciones': 'mantenimiento',
    '/hojasdevida': 'mantenimiento',
    '/insumos': 'insumos',
    '/usosinsumo': 'insumos',
    '/reportes': 'reportes',
    '/reportes/listados': 'reportes',
    '/prestamos': 'prestamos',
    '/hospitalesexternos': 'prestamos',
    '/mapaprestamos': 'prestamos',
    '/usuarios': 'admin'
  };

  // Detectar módulo activo según la URL
  const moduloActivo = moduloDeRuta[location.pathname];

  // Estado: qué módulos están abiertos. Por defecto: solo el de la página actual
  const [abiertos, setAbiertos] = useState({
    patrimonial: moduloActivo === 'patrimonial',
    mantenimiento: moduloActivo === 'mantenimiento',
    insumos: moduloActivo === 'insumos',
    reportes: moduloActivo === 'reportes',
    prestamos: moduloActivo === 'prestamos',
    admin: moduloActivo === 'admin'
  });

  // Cuando cambie la ruta, abrir el módulo correspondiente
  useEffect(() => {
    if (moduloActivo) {
      setAbiertos(prev => ({ ...prev, [moduloActivo]: true }));
    }
  }, [location.pathname, moduloActivo]);

  const toggle = (mod) => {
    setAbiertos(prev => ({ ...prev, [mod]: !prev[mod] }));
  };

  return (
    <aside className={`sidebar-nodus no-print${mobileOpen ? ' sn-open' : ''}`}>

      {/* Logo - ahora clickeable, lleva al home */}
      <NavLink to="/" className="sn-logo" style={{ textDecoration: 'none' }} onClick={onNavigate}>
        <div className="sn-logo-icon">
          <img src="/logonodus.jpg" alt="NODUS" />
        </div>
        <div className="sn-logo-text">
          <div className="sn-logo-name">NODUS</div>
        </div>
      </NavLink>

      {/* Navegación */}
      <nav
        className="sn-nav"
        onClick={(e) => { if (e.target.closest('.sn-item')) onNavigate?.(); }}
      >

        {/* GESTIÓN PATRIMONIAL */}
        {puedeVerModulo(rol, 'patrimonial') && (
          <>
            <div
              className="sn-group-title sn-group-clickable"
              onClick={() => toggle('patrimonial')}
            >
              <IconLayoutDashboard size={14} stroke={2} />
              <span>Gestión patrimonial</span>
            </div>
            {abiertos.patrimonial && (
              <div className="sn-group-content">
                <NavLink to="/activos" className="sn-item">
                  <IconStethoscope size={16} stroke={2} />
                  <span>Activos</span>
                </NavLink>
                <NavLink to="/ubicaciones" className="sn-item">
                  <IconMapPin size={16} stroke={2} />
                  <span>Ubicaciones</span>
                </NavLink>
                {(rol === 'Administrador' || rol === 'Jefe patrimonio') && (
                  <NavLink to="/bajaspatrimoniales" className="sn-item">
                    <IconArchive size={16} stroke={2} />
                    <span>Bajas patrimoniales</span>
                  </NavLink>
                )}
              </div>
            )}
          </>
        )}

        {/* GESTIÓN DE MANTENIMIENTO */}
        {puedeVerModulo(rol, 'mantenimiento') && (
          <>
            <div
              className="sn-group-title sn-group-clickable"
              onClick={() => toggle('mantenimiento')}
            >
              <IconTools size={14} stroke={2} />
              <span>Gestión de mantenimiento</span>
            </div>
            {abiertos.mantenimiento && (
              <div className="sn-group-content">
                <NavLink to="/ordenestrabajo" className="sn-item">
                  <IconClipboardList size={16} stroke={2} />
                  <span>Órdenes de trabajo</span>
                </NavLink>
                <NavLink to="/intervenciones" className="sn-item">
                  <IconSettings size={16} stroke={2} />
                  <span>Tareas de mantenimiento</span>
                </NavLink>
                <NavLink to="/hojasdevida" className="sn-item">
                  <IconFileText size={16} stroke={2} />
                  <span>Hojas de vida</span>
                </NavLink>
              </div>
            )}
          </>
        )}

        {/* GESTIÓN DE INSUMOS */}
        {puedeVerModulo(rol, 'insumos') && (
          <>
            <div
              className="sn-group-title sn-group-clickable"
              onClick={() => toggle('insumos')}
            >
              <IconPackage size={14} stroke={2} />
              <span>Gestión de insumos</span>
            </div>
            {abiertos.insumos && (
              <div className="sn-group-content">
                <NavLink to="/insumos" className="sn-item">
                  <IconMedicalCross size={16} stroke={2} />
                  <span>Insumos</span>
                </NavLink>
                <NavLink to="/usosinsumo" className="sn-item">
                  <IconArrowBarDown size={16} stroke={2} />
                  <span>Uso de insumos</span>
                </NavLink>
              </div>
            )}
          </>
        )}

        {/* GESTIÓN DE PRÉSTAMOS */}
        {puedeVerModulo(rol, 'prestamos') && (
          <>
            <div
              className="sn-group-title sn-group-clickable"
              onClick={() => toggle('prestamos')}
            >
              <IconArrowsExchange size={14} stroke={2} />
              <span>Gestión de préstamos</span>
            </div>
            {abiertos.prestamos && (
              <div className="sn-group-content">
                <NavLink to="/prestamos" className="sn-item">
                  <IconArrowsExchange size={16} stroke={2} />
                  <span>Préstamos</span>
                </NavLink>
                <NavLink to="/hospitalesexternos" className="sn-item">
                  <IconBuildingHospital size={16} stroke={2} />
                  <span>Hospitales externos</span>
                </NavLink>
                <NavLink to="/mapaprestamos" className="sn-item">
                  <IconMap2 size={16} stroke={2} />
                  <span>Mapa de préstamos</span>
                </NavLink>
              </div>
            )}
          </>
        )}

        {/* REPORTES */}
        {puedeVerModulo(rol, 'reportes') && (
          <>
            <div
              className="sn-group-title sn-group-clickable"
              onClick={() => toggle('reportes')}
            >
              <IconChartBar size={14} stroke={2} />
              <span>Reportes</span>
            </div>
            {abiertos.reportes && (
              <div className="sn-group-content">
                <NavLink to="/reportes" end className="sn-item">
                  <IconLayoutDashboard size={16} stroke={2} />
                  <span>Dashboard</span>
                </NavLink>
                <NavLink to="/reportes/listados" className="sn-item">
                  <IconList size={16} stroke={2} />
                  <span>Listados</span>
                </NavLink>
              </div>
            )}
          </>
        )}

        {/* ADMINISTRACIÓN */}
        {puedeVerModulo(rol, 'administracion') && (
          <>
            <div
              className="sn-group-title sn-group-clickable"
              onClick={() => toggle('admin')}
            >
              <IconShieldLock size={14} stroke={2} />
              <span>Administración</span>
            </div>
            {abiertos.admin && (
              <div className="sn-group-content">
                <NavLink to="/usuarios" className="sn-item">
                  <IconUsers size={16} stroke={2} />
                  <span>Usuarios</span>
                </NavLink>
              </div>
            )}
          </>
        )}

      </nav>

      {/* Footer con usuario */}
      <div className="sn-footer">
        <div className="sn-avatar">{iniciales}</div>
        <div className="sn-user-info">
          <div className="sn-user-name">{auth.usuario.nombre}</div>
          <div className="sn-user-role">{auth.usuario.rol}</div>
        </div>
        <button className="sn-logout-btn" onClick={onLogout} title="Cerrar sesión">
          <IconLogout size={18} stroke={2} />
        </button>
      </div>

    </aside>
  );
}

function RutaProtegida({ auth, modulo, children }) {
  const rol = auth?.usuario?.rol;
  if (!puedeVerModulo(rol, modulo)) {
    return <Navigate to="/" replace />;
  }
  return children;
}

function App() {
  const [auth, setAuth] = useState(null);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  const handleLogin = (data) => { setAuth(data); };
  const handleLogout = () => { setAuth(null); };

  if (!auth) {
    return <Login onLogin={handleLogin} />;
  }

  return (
    <BrowserRouter>
      <div className="app">
        <button
          className="sn-hamburger no-print"
          onClick={() => setMobileNavOpen(o => !o)}
          aria-label={mobileNavOpen ? 'Cerrar menú' : 'Abrir menú'}
        >
          {mobileNavOpen ? <IconX size={22} /> : <IconMenu2 size={22} />}
        </button>
        {mobileNavOpen && (
          <div className="sn-overlay no-print" onClick={() => setMobileNavOpen(false)} />
        )}
        <Sidebar
          auth={auth}
          onLogout={handleLogout}
          mobileOpen={mobileNavOpen}
          onNavigate={() => setMobileNavOpen(false)}
        />
        <main className="content">
          <Routes>
            <Route path="/" element={<Bienvenida auth={auth} />} />
            <Route path="/marcas" element={<Marcas />} />
            <Route path="/sectores" element={<Sectores />} />
            <Route path="/servicios" element={<Servicios />} />
            <Route path="/activos" element={<RutaProtegida auth={auth} modulo="patrimonial"><Activos /></RutaProtegida>} />
            <Route path="/ficha/:idEquipo" element={<FichaEquipo auth={auth} />} />
            <Route path="/equipos" element={<Navigate to="/activos" />} />
            <Route path="/ubicaciones" element={<RutaProtegida auth={auth} modulo="patrimonial"><Ubicaciones /></RutaProtegida>} />
            <Route path="/insumos" element={<RutaProtegida auth={auth} modulo="insumos"><Insumos /></RutaProtegida>} />
            <Route path="/ordenestrabajo" element={<RutaProtegida auth={auth} modulo="mantenimiento"><OrdenesTrabajo /></RutaProtegida>} />
            <Route path="/intervenciones" element={<RutaProtegida auth={auth} modulo="mantenimiento"><Intervenciones /></RutaProtegida>} />
            <Route path="/usosinsumo" element={<RutaProtegida auth={auth} modulo="insumos"><UsosInsumo /></RutaProtegida>} />
            <Route path="/alertas" element={<Alertas auth={auth} />} />
            <Route path="/bajaspatrimoniales" element={<RutaProtegida auth={auth} modulo="patrimonial"><BajasPatrimoniales /></RutaProtegida>} />
            <Route path="/hojasdevida" element={<RutaProtegida auth={auth} modulo="mantenimiento"><HojasDeVida /></RutaProtegida>} />
            <Route path="/usuarios" element={<RutaProtegida auth={auth} modulo="administracion"><Usuarios /></RutaProtegida>} />
            <Route path="/prestamos" element={<RutaProtegida auth={auth} modulo="prestamos"><Prestamos auth={auth} /></RutaProtegida>} />
            <Route path="/hospitalesexternos" element={<RutaProtegida auth={auth} modulo="prestamos"><HospitalesExternos /></RutaProtegida>} />
            <Route path="/mapaprestamos" element={<RutaProtegida auth={auth} modulo="prestamos"><MapaPrestamos /></RutaProtegida>} />
            <Route path="*" element={<Navigate to="/" />} />
            <Route path="/reportes" element={<RutaProtegida auth={auth} modulo="reportes"><Reportes /></RutaProtegida>} />
            <Route path="/reportes/listados" element={<RutaProtegida auth={auth} modulo="reportes"><Reportes /></RutaProtegida>} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}

export default App;
