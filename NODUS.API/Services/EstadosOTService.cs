using Microsoft.EntityFrameworkCore;
using NODUS.Data;

namespace NODUS.API.Services
{
    // Deriva automáticamente el estado de las OT (desde sus tareas) y de los equipos (desde sus OT).
    // No abre transacciones: el que llama envuelve todo en una sola transacción.
    public class EstadosOTService
    {
        // IDs de estados del equipo (matchean con el seed del DbContext)
        public const int ID_ESTADO_EN_SERVICIO = 1;
        public const int ID_ESTADO_EN_MANTENIMIENTO = 2;
        public const int ID_ESTADO_FUERA_DE_SERVICIO = 3;
        public const int ID_ESTADO_EN_PRESTAMO = 4;
        public const int ID_ESTADO_BAJA_PATRIMONIAL = 5;

        public const string MSJ_NO_CERRADA_SIN_APROBADO =
            "La OT no se cerró porque el resultado de seguridad eléctrica no es Aprobado.";
        public const string MSJ_REABIERTA_POR_DESAPROBADO =
            "La OT volvió a 'En curso' porque el resultado fue Desaprobado.";

        // "Reprobado" es el nombre viejo de "Desaprobado" (quedan registros guardados así)
        public static bool EsDesaprobado(string resultado) => resultado == "Desaprobado" || resultado == "Reprobado";

        private readonly NodusDbContext _db;
        private readonly ILogger<EstadosOTService> _logger;

        public EstadosOTService(NodusDbContext db, ILogger<EstadosOTService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public static bool EsAbierta(string estado) => estado == "Pendiente" || estado == "En curso";

        // REGLA DE NEGOCIO: el estado de la OT lo define su tarea de mantenimiento más reciente
        // (por fecha de inicio y, si empatan, por ID más alto):
        // sin tareas -> Pendiente; la más reciente con fecha de fin y resultado "Aprobado" -> Cerrada (US-10);
        // cualquier otro caso (Desaprobado, Aprobado con observaciones, sin fecha de fin) -> En curso.
        // Así una tarea Desaprobado reabre una OT Cerrada. Las OT Anuladas no se tocan.
        // Guarda la OT y después recalcula el equipo, así la consulta de OT abiertas ve el estado nuevo.
        // Si se pasa la tarea recién guardada y es la más reciente, devuelve un aviso para el usuario.
        public async Task<string?> RecalcularEstadoOT(int idOT, int? idTareaGuardada = null)
        {
            var ot = await _db.OrdenesTrabajo.FindAsync(idOT);
            if (ot == null || ot.Estado == "Anulada") return null;

            var ultima = await _db.Intervenciones
                .Where(i => i.IdOT == idOT)
                .OrderByDescending(i => i.Fecha).ThenByDescending(i => i.IdIntervencion)
                .Select(i => new { i.IdIntervencion, i.FechaFin, i.ResultadoSegElectrica })
                .FirstOrDefaultAsync();

            string? aviso = null;
            string estadoAnterior = ot.Estado;
            if (ultima == null)
            {
                ot.Estado = "Pendiente";
                ot.FechaCierre = null;
            }
            else if (ultima.FechaFin.HasValue && ultima.ResultadoSegElectrica == "Aprobado")
            {
                ot.Estado = "Cerrada";
                ot.FechaCierre = ultima.FechaFin;
            }
            else
            {
                ot.Estado = "En curso";
                ot.FechaCierre = null;
                if (ultima.IdIntervencion == idTareaGuardada)
                {
                    if (EsDesaprobado(ultima.ResultadoSegElectrica))
                        aviso = MSJ_REABIERTA_POR_DESAPROBADO;
                    else if (ultima.FechaFin.HasValue)
                        aviso = MSJ_NO_CERRADA_SIN_APROBADO;
                }
            }

            // TEMP: log de diagnóstico del recálculo de estado
            _logger.LogInformation("[RecalcularEstadoOT] OT {IdOT}: última tarea {Ultima} ({Resultado}, fin {Fin}) -> estado {Anterior} => {Final}",
                idOT, ultima?.IdIntervencion, ultima?.ResultadoSegElectrica, ultima?.FechaFin, estadoAnterior, ot.Estado);

            await _db.SaveChangesAsync();
            await RecalcularEstadoEquipo(ot.IdEquipo);
            await _db.SaveChangesAsync();
            return aviso;
        }

        // REGLA DE NEGOCIO: el estado del equipo se deriva de sus OT abiertas.
        // Alguna "En curso" -> En mantenimiento; si no, alguna "Pendiente" -> Fuera de servicio;
        // ninguna abierta -> En servicio. Cerradas y anuladas no cuentan.
        // No toca equipos dados de baja ni en préstamo (esos estados los gestionan sus módulos).
        // Consulta la base: los cambios de las OT tienen que estar guardados antes de llamarlo.
        public async Task RecalcularEstadoEquipo(int idEquipo)
        {
            var equipo = await _db.Equipos.FindAsync(idEquipo);
            if (equipo == null
                || equipo.IdEstado == ID_ESTADO_BAJA_PATRIMONIAL
                || equipo.IdEstado == ID_ESTADO_EN_PRESTAMO)
            {
                // TEMP: log de diagnóstico del recálculo de estado
                _logger.LogInformation("[RecalcularEstadoEquipo] Equipo {IdEquipo}: sin cambios (estado actual {IdEstado})",
                    idEquipo, equipo?.IdEstado);
                return;
            }

            var abiertas = await _db.OrdenesTrabajo
                .Where(o => o.IdEquipo == idEquipo && (o.Estado == "Pendiente" || o.Estado == "En curso"))
                .Select(o => new { o.IdOT, o.Estado })
                .ToListAsync();

            int estadoAnterior = equipo.IdEstado;
            equipo.IdEstado = abiertas.Any(o => o.Estado == "En curso") ? ID_ESTADO_EN_MANTENIMIENTO
                            : abiertas.Any(o => o.Estado == "Pendiente") ? ID_ESTADO_FUERA_DE_SERVICIO
                            : ID_ESTADO_EN_SERVICIO;

            // TEMP: log de diagnóstico del recálculo de estado
            _logger.LogInformation("[RecalcularEstadoEquipo] Equipo {IdEquipo}: OT abiertas [{Abiertas}] -> estado {Anterior} => {Final}",
                idEquipo, string.Join(", ", abiertas.Select(o => $"#{o.IdOT} {o.Estado}")), estadoAnterior, equipo.IdEstado);
        }
    }
}
