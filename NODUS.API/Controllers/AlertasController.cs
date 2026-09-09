using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlertasController : ControllerBase
    {
        private readonly NodusDbContext _db;

        private static readonly string[] TIPOS_VALIDOS = {
            "Garantía por vencer",
            "Mantenimiento preventivo",
            "Falla recurrente",
            "Calibración pendiente",
            "Stock crítico",
            "Otro"
        };

        private static readonly string[] ESTADOS_VALIDOS = {
            "No leída", "Leída", "Resuelta", "Descartada"
        };

        // Días antes de vencimiento de garantía para disparar alerta
        private const int DIAS_AVISO_GARANTIA = 30;

        public AlertasController(NodusDbContext db)
        {
            _db = db;
        }

        // GET: api/alertas
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Alerta>>> GetAlertas()
        {
            var alertas = await _db.Alertas
                .Include(a => a.Equipo).ThenInclude(e => e!.Marca)
                .Include(a => a.Usuario)
                .OrderByDescending(a => a.FechaGeneracion)
                .ToListAsync();
            return Ok(alertas);
        }

        // GET: api/alertas/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Alerta>> GetAlerta(int id)
        {
            var a = await _db.Alertas
                .Include(x => x.Equipo).ThenInclude(e => e!.Marca)
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.IdAlerta == id);
            if (a == null) return NotFound();
            return Ok(a);
        }

        // GET: api/alertas/pendientes
        // Útil para badge contador en sidebar
        [HttpGet("pendientes")]
        public async Task<ActionResult<IEnumerable<Alerta>>> GetPendientes()
        {
            var pendientes = await _db.Alertas
                .Include(a => a.Equipo)
                .Where(a => a.Estado == "No leída" || a.Estado == "Leída")
                .OrderByDescending(a => a.FechaGeneracion)
                .ToListAsync();
            return Ok(pendientes);
        }

        // POST: api/alertas
        [HttpPost]
        public async Task<ActionResult<Alerta>> PostAlerta(Alerta alerta)
        {
            if (!TIPOS_VALIDOS.Contains(alerta.Tipo))
                return BadRequest($"Tipo de alerta inválido. Valores permitidos: {string.Join(", ", TIPOS_VALIDOS)}.");
            if (!ESTADOS_VALIDOS.Contains(alerta.Estado))
                alerta.Estado = "No leída";

            alerta.FechaGeneracion = DateTime.SpecifyKind(alerta.FechaGeneracion, DateTimeKind.Utc);

            _db.Alertas.Add(alerta);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAlerta), new { id = alerta.IdAlerta }, alerta);
        }

        // PUT: api/alertas/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAlerta(int id, Alerta alerta)
        {
            if (id != alerta.IdAlerta) return BadRequest();
            if (!TIPOS_VALIDOS.Contains(alerta.Tipo))
                return BadRequest("Tipo inválido.");
            if (!ESTADOS_VALIDOS.Contains(alerta.Estado))
                return BadRequest("Estado inválido.");

            alerta.FechaGeneracion = DateTime.SpecifyKind(alerta.FechaGeneracion, DateTimeKind.Utc);
            _db.Entry(alerta).State = EntityState.Modified;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _db.Alertas.AnyAsync(a => a.IdAlerta == id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/alertas/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlerta(int id)
        {
            var a = await _db.Alertas.FindAsync(id);
            if (a == null) return NotFound();
            _db.Alertas.Remove(a);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ============================================================
        // ENDPOINTS DE GENERACIÓN AUTOMÁTICA (el oro para tesis)
        // ============================================================

        // POST: api/alertas/generar-automaticas
        // Detecta condiciones del sistema y genera alertas automáticamente
        [HttpPost("generar-automaticas")]
        public async Task<ActionResult<object>> GenerarAutomaticas([FromQuery] int idUsuarioReportador)
        {
            // Validar que el usuario exista
            var usuario = await _db.Usuarios.FindAsync(idUsuarioReportador);
            if (usuario == null)
                return BadRequest("Usuario reportador no válido.");

            int creadas = 0;
            int omitidas = 0;
            var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            var limiteGarantia = ahora.AddDays(DIAS_AVISO_GARANTIA);

            // 1) Equipos con garantía por vencer en menos de 30 días
            var equiposGarantia = await _db.Equipos
                .Where(e => e.VencimientoGarantia.HasValue
                         && e.VencimientoGarantia.Value <= limiteGarantia
                         && e.VencimientoGarantia.Value >= ahora)
                .ToListAsync();

            foreach (var eq in equiposGarantia)
            {
                // Evitar duplicar: no crear si ya hay una alerta activa para este equipo de este tipo
                bool yaExiste = await _db.Alertas.AnyAsync(a =>
                    a.IdEquipo == eq.IdEquipo
                    && a.Tipo == "Garantía por vencer"
                    && (a.Estado == "No leída" || a.Estado == "Leída"));

                if (yaExiste) { omitidas++; continue; }

                _db.Alertas.Add(new Alerta
                {
                    Tipo = "Garantía por vencer",
                    FechaGeneracion = ahora,
                    Estado = "No leída",
                    IdEquipo = eq.IdEquipo,
                    IdUsuario = idUsuarioReportador
                });
                creadas++;
            }

            // 2) Insumos con stock por debajo del mínimo
            var insumosCriticos = await _db.Insumos
                .Where(i => i.StockActual < i.StockMinimo)
                .ToListAsync();

            foreach (var ins in insumosCriticos)
            {
                // Como "Stock crítico" no está atado a equipo, comparamos por tipo y estado activo
                // Para no duplicar, omitimos si ya hay una alerta de stock crítico activa del último día
                var hace24hs = ahora.AddHours(-24);
                bool yaExiste = await _db.Alertas.AnyAsync(a =>
                    a.Tipo == "Stock crítico"
                    && a.FechaGeneracion >= hace24hs
                    && (a.Estado == "No leída" || a.Estado == "Leída"));

                if (yaExiste) { omitidas++; break; } // una sola alerta general de stock crítico

                _db.Alertas.Add(new Alerta
                {
                    Tipo = "Stock crítico",
                    FechaGeneracion = ahora,
                    Estado = "No leída",
                    IdEquipo = null, // no está atado a un equipo
                    IdUsuario = idUsuarioReportador
                });
                creadas++;
                break;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                creadas,
                omitidas,
                mensaje = creadas == 0
                    ? "No se detectaron nuevas condiciones de alerta."
                    : $"Se generaron {creadas} alerta(s) automática(s). {omitidas} omitida(s) por ya existir."
            });
        }
    }
}
