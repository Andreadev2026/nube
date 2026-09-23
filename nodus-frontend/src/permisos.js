// Sistema centralizado de permisos por rol.
// Para dar o sacar un acceso, modificar solo este archivo.

export const ACCESO_MODULOS = {
  'Administrador':      ['patrimonial', 'mantenimiento', 'insumos', 'prestamos', 'reportes', 'administracion'],
  'Jefe patrimonio':     ['patrimonial', 'mantenimiento', 'insumos', 'prestamos', 'reportes'],
  'Técnico biomédico':   ['patrimonial', 'mantenimiento', 'prestamos', 'reportes'],
  'Médico':              ['mantenimiento'],
  'Residente':           ['mantenimiento'],
  'Enfermero':           ['mantenimiento'],
  'Jefe servicio':       ['patrimonial', 'mantenimiento', 'reportes'],
  'Auditor ANMAT':       ['reportes'],
};

export function puedeVerModulo(rol, modulo) {
  return ACCESO_MODULOS[rol]?.includes(modulo) ?? false;
}

export const ACCIONES = {
  'crear_ot': ['Administrador', 'Jefe patrimonio', 'Técnico biomédico', 'Médico', 'Residente', 'Enfermero', 'Jefe servicio', 'Auditor ANMAT'],
  'editar_activo': ['Administrador', 'Jefe patrimonio', 'Técnico biomédico'],
  'gestionar_usuarios': ['Administrador'],
};

export function puedeHacer(rol, accion) {
  return ACCIONES[accion]?.includes(rol) ?? false;
}
