using Microsoft.EntityFrameworkCore;
using NODUS.API.DTOs;
using NODUS.Data;
using NODUS.Models;
using Npgsql;

namespace NODUS.API.Services
{
    // Resultado de una operación del servicio: el controller lo traduce a HTTP
    public enum ResultadoReporte { Ok, NoEncontrado, Invalido, Duplicado, Conflicto }

    public record RespuestaReporte(ResultadoReporte Resultado, string? Mensaje = null, ReporteFalla? Reporte = null);

    public class ReportesFallaService
    {
        public const string MSJ_DUPLICADO =
            "Este equipo ya tiene una falla reportada hoy. Mantenimiento ya fue notificado.";

        public const int MAX_NOMBRE = 100;
        public const int MAX_LEGAJO = 30;
        public const int MAX_DESCRIPCION = 1000;

        private readonly NodusDbContext _db;
        private readonly EstadosOTService _estados;

        public ReportesFallaService(NodusDbContext db, EstadosOTService estados)
        {
            _db = db;
            _estados = estados;
        }

        // "El mismo día" es el día calendario en Argentina, no en UTC
        public static DateOnly HoyArgentina() => DateOnly.FromDateTime(ZonaHoraria.AhoraArgentina());

        // Los descartados no cuentan: después de descartar, el equipo se puede volver a reportar ese día
        public Task<bool> ReportadoHoy(int idEquipo)
        {
            var hoy = HoyArgentina();
            return _db.ReportesFalla.AnyAsync(r =>
                r.IdEquipo == idEquipo && r.FechaDia == hoy && r.Estado != ReporteFalla.ESTADO_DESCARTADO);
        }

        public async Task<FichaQrDto?> ObtenerFichaQr(int idEquipo)
        {
            var ficha = await _db.Equipos
                .Where(e => e.IdEquipo == idEquipo)
                .Select(e => new FichaQrDto
                {
                    IdEquipo = e.IdEquipo,
                    Nombre = e.Modelo,
                    NroSerie = e.NroSerie,
                    Estado = e.EstadoEquipo!.Nombre,
                    ColorEstado = e.EstadoEquipo!.ColorSemaforo,
                    Marca = e.Marca!.Nombre,
                    Modelo = e.Modelo,
                    TipoActivo = e.TipoActivo!.Nombre,
                    // En préstamo no se informa a qué hospital; si no, sector y servicio del último traslado
                    Ubicacion = e.IdEstado == EstadosOTService.ID_ESTADO_EN_PRESTAMO
                        ? "En préstamo externo"
                        : e.Ubicaciones
                            .OrderByDescending(u => u.FechaTraslado).ThenByDescending(u => u.IdUbicacion)
                            .Select(u => u.SectorNuevo!.Nombre + " · " + u.Servicio!.Nombre)
                            .FirstOrDefault(),
                    Origen = e.Marca!.PaisOrigen,
                    FechaAlta = e.FechaAdquisicion,
                    Alertas = e.Alertas
                        .Where(a => a.Estado == "No leída" || a.Estado == "Leída")
                        .Select(a => a.Tipo)
                        .Distinct()
                        .ToList(),
                    Componentes = e.EquipoComponentes.Select(c => c.Componente).ToList()
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (ficha != null)
                ficha.ReportadoHoy = await ReportadoHoy(idEquipo);
            return ficha;
        }

        // Búsqueda manual cuando no se puede escanear el QR: primero por número de serie exacto
        // (sin distinguir mayúsculas), después por ID. Solo devuelve el ID para abrir la ficha QR.
        public async Task<int?> BuscarIdEquipo(string codigo)
        {
            var texto = codigo.Trim();
            if (texto.Length == 0) return null;

            var serie = texto.ToLower();
            var porSerie = await _db.Equipos
                .Where(e => e.NroSerie.Trim().ToLower() == serie)
                .OrderBy(e => e.IdEquipo)
                .Select(e => (int?)e.IdEquipo)
                .FirstOrDefaultAsync();
            if (porSerie != null) return porSerie;

            if (int.TryParse(texto, out var id) && await _db.Equipos.AnyAsync(e => e.IdEquipo == id))
                return id;
            return null;
        }

        public async Task<RespuestaReporte> Crear(int idEquipo, int idUsuario, ReporteFallaInputDto input)
        {
            if (!await _db.Equipos.AnyAsync(e => e.IdEquipo == idEquipo))
                return new(ResultadoReporte.NoEncontrado, "El equipo no existe.");

            var nombre = input.NombreCompleto?.Trim() ?? "";
            var legajo = input.Legajo?.Trim() ?? "";
            var descripcion = input.Descripcion?.Trim() ?? "";

            if (nombre.Length == 0) return new(ResultadoReporte.Invalido, "El nombre completo es obligatorio.");
            if (legajo.Length == 0) return new(ResultadoReporte.Invalido, "El legajo o matrícula es obligatorio.");
            if (descripcion.Length == 0) return new(ResultadoReporte.Invalido, "La descripción del problema es obligatoria.");
            if (nombre.Length > MAX_NOMBRE)
                return new(ResultadoReporte.Invalido, $"El nombre completo no puede superar los {MAX_NOMBRE} caracteres.");
            if (legajo.Length > MAX_LEGAJO)
                return new(ResultadoReporte.Invalido, $"El legajo o matrícula no puede superar los {MAX_LEGAJO} caracteres.");
            if (descripcion.Length > MAX_DESCRIPCION)
                return new(ResultadoReporte.Invalido, $"La descripción no puede superar los {MAX_DESCRIPCION} caracteres.");

            // Primer nivel de la regla "un reporte por equipo por día"
            if (await ReportadoHoy(idEquipo))
                return new(ResultadoReporte.Duplicado, MSJ_DUPLICADO);

            var ahora = DateTime.UtcNow;
            var reporte = new ReporteFalla
            {
                IdEquipo = idEquipo,
                IdUsuario = idUsuario,
                NombreCompleto = nombre,
                Legajo = legajo,
                Descripcion = descripcion,
                FechaReporte = ahora,
                FechaDia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(ahora, ZonaHoraria.Argentina)),
                Estado = ReporteFalla.ESTADO_PENDIENTE
            };
            _db.ReportesFalla.Add(reporte);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ReporteFalla.INDICE_UNO_POR_DIA
            })
            {
                // Segundo nivel: otro envío se guardó entre el chequeo y este insert
                _db.Entry(reporte).State = EntityState.Detached;
                return new(ResultadoReporte.Duplicado, MSJ_DUPLICADO);
            }

            return new(ResultadoReporte.Ok, Reporte: reporte);
        }

        public async Task<List<ReporteFallaDto>> Listar(string? estado)
        {
            var query = _db.ReportesFalla.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(estado))
                query = query.Where(r => r.Estado == estado);

            return await query
                .OrderByDescending(r => r.FechaReporte)
                .Select(r => new ReporteFallaDto
                {
                    IdReporteFalla = r.IdReporteFalla,
                    IdEquipo = r.IdEquipo,
                    Equipo = r.Equipo!.Modelo,
                    NroSerie = r.Equipo!.NroSerie,
                    NombreCompleto = r.NombreCompleto,
                    Legajo = r.Legajo,
                    Usuario = r.Usuario!.Nombre,
                    Descripcion = r.Descripcion,
                    FechaReporte = r.FechaReporte,
                    Estado = r.Estado,
                    IdOT = r.IdOT,
                    IdServicioSugerido = r.Equipo!.Ubicaciones
                        .OrderByDescending(u => u.FechaTraslado).ThenByDescending(u => u.IdUbicacion)
                        .Select(u => (int?)u.IdServicio)
                        .FirstOrDefault()
                })
                .ToListAsync();
        }

        // Crea la OT "Pendiente" (el equipo pasa a "Fuera de servicio" por EstadosOTService, como al
        // crear una OT a mano) y marca el reporte "Convertido en OT". Todo en una transacción.
        public async Task<RespuestaReporte> ConvertirEnOT(int idReporte, int idUsuario, int? idServicio)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var reporte = await BloquearReporte(idReporte);
            if (reporte == null) return new(ResultadoReporte.NoEncontrado, "El reporte no existe.");
            if (reporte.Estado != ReporteFalla.ESTADO_PENDIENTE)
                return new(ResultadoReporte.Conflicto, $"El reporte ya está en estado \"{reporte.Estado}\".");

            var equipo = await _db.Equipos.FindAsync(reporte.IdEquipo);
            if (equipo == null) return new(ResultadoReporte.NoEncontrado, "El equipo no existe.");

            // Mismas validaciones que OrdenesTrabajoController.PostOT
            if (equipo.IdEstado == EstadosOTService.ID_ESTADO_BAJA_PATRIMONIAL)
                return new(ResultadoReporte.Invalido, "Error: Equipo inactivo por baja técnica definitiva.");
            if (equipo.IdEstado == EstadosOTService.ID_ESTADO_EN_PRESTAMO)
                return new(ResultadoReporte.Invalido,
                    "El equipo está en préstamo. Registrá la devolución antes de crear una orden de trabajo.");

            idServicio ??= await _db.Ubicaciones
                .Where(u => u.IdEquipo == equipo.IdEquipo)
                .OrderByDescending(u => u.FechaTraslado).ThenByDescending(u => u.IdUbicacion)
                .Select(u => (int?)u.IdServicio)
                .FirstOrDefaultAsync();
            if (idServicio == null)
                return new(ResultadoReporte.Invalido, "El equipo no tiene ubicación registrada: elegí el servicio de la OT.");
            if (!await _db.Servicios.AnyAsync(s => s.IdServicio == idServicio))
                return new(ResultadoReporte.Invalido, "El servicio seleccionado no existe.");

            var ot = new OrdenTrabajo
            {
                FechaApertura = DateTime.UtcNow,
                Tipo = "Correctiva",
                Sintoma = $"Reporte de falla #{reporte.IdReporteFalla} — {reporte.NombreCompleto} " +
                          $"(legajo/matrícula {reporte.Legajo}): {reporte.Descripcion}",
                Estado = "Pendiente",
                IdEquipo = equipo.IdEquipo,
                IdServicio = idServicio.Value,
                IdUsuario = idUsuario
            };
            _db.OrdenesTrabajo.Add(ot);
            await _db.SaveChangesAsync();

            reporte.IdOT = ot.IdOT;
            reporte.Estado = ReporteFalla.ESTADO_CONVERTIDO;
            await _estados.RecalcularEstadoEquipo(equipo.IdEquipo);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new(ResultadoReporte.Ok, Reporte: reporte);
        }

        public async Task<RespuestaReporte> Descartar(int idReporte)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var reporte = await BloquearReporte(idReporte);
            if (reporte == null) return new(ResultadoReporte.NoEncontrado, "El reporte no existe.");
            if (reporte.Estado != ReporteFalla.ESTADO_PENDIENTE)
                return new(ResultadoReporte.Conflicto, $"El reporte ya está en estado \"{reporte.Estado}\".");

            reporte.Estado = ReporteFalla.ESTADO_DESCARTADO;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return new(ResultadoReporte.Ok, Reporte: reporte);
        }

        // Bloquea la fila hasta el fin de la transacción: dos usuarios no pueden convertir/descartar el mismo reporte a la vez
        // (ToList y no FirstOrDefault, para que EF no envuelva el FOR UPDATE en una subconsulta)
        private async Task<ReporteFalla?> BloquearReporte(int idReporte) =>
            (await _db.ReportesFalla
                .FromSqlInterpolated($"SELECT * FROM reporte_falla WHERE id_reporte_falla = {idReporte} FOR UPDATE")
                .ToListAsync())
            .FirstOrDefault();
    }
}
