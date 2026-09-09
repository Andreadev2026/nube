using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdenesTrabajoController : ControllerBase
    {
        private readonly NodusDbContext _db;

        // IDs de estados del equipo (matchean con el seed del DbContext)
        private const int ESTADO_EN_SERVICIO = 1;
        private const int ESTADO_EN_MANTENIMIENTO = 2;

        // Estados válidos de la OT
        private static readonly string[] ESTADOS_VALIDOS = { "Pendiente", "En curso", "Cerrada", "Anulada" };
        private static readonly string[] TIPOS_VALIDOS = { "Correctiva", "Preventiva", "Calibración", "Verificación de seguridad eléctrica" };

        public OrdenesTrabajoController(NodusDbContext db)
        {
            _db = db;
        }

        // GET: api/ordenestrabajo
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrdenTrabajo>>> GetOTs()
        {
            var ots = await _db.OrdenesTrabajo
                .Include(o => o.Equipo).ThenInclude(e => e!.Marca)
                .Include(o => o.Equipo).ThenInclude(e => e!.TipoActivo)
                .Include(o => o.Servicio)
                .Include(o => o.Usuario)
                .OrderByDescending(o => o.FechaApertura)
                .ToListAsync();
            return Ok(ots);
        }

        // GET: api/ordenestrabajo/5
        [HttpGet("{id}")]
        public async Task<ActionResult<OrdenTrabajo>> GetOT(int id)
        {
            var ot = await _db.OrdenesTrabajo
                .Include(o => o.Equipo).ThenInclude(e => e!.Marca)
                .Include(o => o.Equipo).ThenInclude(e => e!.TipoActivo)
                .Include(o => o.Servicio)
                .Include(o => o.Usuario)
                .Include(o => o.Intervenciones)
                .Include(o => o.UsosInsumo).ThenInclude(u => u.Insumo)
                .FirstOrDefaultAsync(o => o.IdOT == id);

            if (ot == null) return NotFound();
            return Ok(ot);
        }

        // GET: api/ordenestrabajo/abiertas
        [HttpGet("abiertas")]
        public async Task<ActionResult<IEnumerable<OrdenTrabajo>>> GetAbiertas()
        {
            var ots = await _db.OrdenesTrabajo
                .Include(o => o.Equipo)
                .Include(o => o.Servicio)
                .Where(o => o.Estado == "Pendiente" || o.Estado == "En curso")
                .OrderBy(o => o.FechaApertura)
                .ToListAsync();
            return Ok(ots);
        }

        // POST: api/ordenestrabajo
        [HttpPost]
        public async Task<ActionResult<OrdenTrabajo>> PostOT(OrdenTrabajo ot)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(ot.Tipo) || !TIPOS_VALIDOS.Contains(ot.Tipo))
                return BadRequest($"Tipo de OT inválido. Valores permitidos: {string.Join(", ", TIPOS_VALIDOS)}.");
            if (string.IsNullOrWhiteSpace(ot.Sintoma))
                return BadRequest("El síntoma o descripción del trabajo es obligatorio.");
            if (!ESTADOS_VALIDOS.Contains(ot.Estado))
                ot.Estado = "Pendiente";

            ot.FechaApertura = DateTime.SpecifyKind(ot.FechaApertura, DateTimeKind.Utc);
            if (ot.FechaCierre.HasValue)
                ot.FechaCierre = DateTime.SpecifyKind(ot.FechaCierre.Value, DateTimeKind.Utc);

            // Validar que el equipo exista (cargando su estado para US-05)
            var equipo = await _db.Equipos
                .Include(e => e.EstadoEquipo)
                .FirstOrDefaultAsync(e => e.IdEquipo == ot.IdEquipo);

            if (equipo == null) return BadRequest("El equipo seleccionado no existe.");

            // ═══════════════════════════════════════════════════════════════════
            // US-05: OPERACIÓN DE ACTIVOS DADOS DE BAJA (Criterio de Baja)
            // ═══════════════════════════════════════════════════════════════════
            if (equipo.EstadoEquipo?.InhabilitaOperacion == true)
            {
                return BadRequest("Error: Equipo inactivo por baja técnica definitiva.");
            }

            // REGLA DE NEGOCIO: al crear una OT abierta, el equipo pasa a "En mantenimiento"
            if (ot.Estado == "Pendiente" || ot.Estado == "En curso")
            {
                equipo.IdEstado = ESTADO_EN_MANTENIMIENTO;
            }

            _db.OrdenesTrabajo.Add(ot);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOT), new { id = ot.IdOT }, ot);
        }

        // PUT: api/ordenestrabajo/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOT(int id, OrdenTrabajo ot)
        {
            if (id != ot.IdOT) return BadRequest();
            if (!TIPOS_VALIDOS.Contains(ot.Tipo))
                return BadRequest($"Tipo de OT inválido.");
            if (!ESTADOS_VALIDOS.Contains(ot.Estado))
                return BadRequest("Estado de OT inválido.");

            var otAnterior = await _db.OrdenesTrabajo.AsNoTracking().FirstOrDefaultAsync(o => o.IdOT == id);
            if (otAnterior == null) return NotFound();

            // ═══════════════════════════════════════════════════════════════════
            // PBI-05: INMUTABILIDAD HISTÓRICA (Res. 2725/2023 - Tecnovigilancia)
            // ═══════════════════════════════════════════════════════════════════
            if (otAnterior.Estado == "Cerrada" || otAnterior.Estado == "Anulada")
            {
                return BadRequest("Consulta: Los registros finalizados son inalterables por auditoría.");
            }

            // ═══════════════════════════════════════════════════════════════════
            // US-05: OPERACIÓN DE ACTIVOS DADOS DE BAJA
            // ═══════════════════════════════════════════════════════════════════
            if (ot.IdEquipo != otAnterior.IdEquipo)
            {
                var equipoNuevo = await _db.Equipos
                    .Include(e => e.EstadoEquipo)
                    .FirstOrDefaultAsync(e => e.IdEquipo == ot.IdEquipo);

                if (equipoNuevo == null)
                    return BadRequest("El equipo seleccionado no existe.");

                if (equipoNuevo.EstadoEquipo?.InhabilitaOperacion == true)
                {
                    return BadRequest("Error: Equipo inactivo por baja técnica definitiva.");
                }
            }

            // ═══════════════════════════════════════════════════════════════════
            // US-10: RIESGO ELÉCTRICO CRÍTICO (Política de Bioingeniería)
            // ═══════════════════════════════════════════════════════════════════
            bool seEstaCerrando = (otAnterior.Estado == "Pendiente" || otAnterior.Estado == "En curso")
                              && ot.Estado == "Cerrada";

            if (seEstaCerrando)
            {
                bool tieneSegElectricaAprobada = await _db.Intervenciones
                    .AnyAsync(i => i.IdOT == id && i.ResultadoSegElectrica == "Aprobado");

                if (!tieneSegElectricaAprobada)
                {
                    return BadRequest("Acción Denegada: Es obligatorio registrar el resultado de seguridad eléctrica.");
                }
            }

            ot.FechaApertura = DateTime.SpecifyKind(ot.FechaApertura, DateTimeKind.Utc);

            if (ot.Estado == "Cerrada" && !ot.FechaCierre.HasValue)
            {
                ot.FechaCierre = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            }
            else if (ot.FechaCierre.HasValue)
            {
                ot.FechaCierre = DateTime.SpecifyKind(ot.FechaCierre.Value, DateTimeKind.Utc);
            }

            bool seCerro = (otAnterior.Estado == "Pendiente" || otAnterior.Estado == "En curso")
                        && (ot.Estado == "Cerrada" || ot.Estado == "Anulada");

            bool seReabrio = (otAnterior.Estado == "Cerrada" || otAnterior.Estado == "Anulada")
                          && (ot.Estado == "Pendiente" || ot.Estado == "En curso");

            _db.Entry(ot).State = EntityState.Modified;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _db.OrdenesTrabajo.AnyAsync(o => o.IdOT == id)) return NotFound();
                throw;
            }

            var equipo = await _db.Equipos.FindAsync(ot.IdEquipo);
            if (equipo != null)
            {
                if (seCerro)
                {
                    bool tieneOtrasAbiertas = await _db.OrdenesTrabajo
                        .AnyAsync(o => o.IdEquipo == ot.IdEquipo
                                    && o.IdOT != ot.IdOT
                                    && (o.Estado == "Pendiente" || o.Estado == "En curso"));

                    if (!tieneOtrasAbiertas)
                    {
                        equipo.IdEstado = ESTADO_EN_SERVICIO;
                        await _db.SaveChangesAsync();
                    }
                }
                else if (seReabrio)
                {
                    equipo.IdEstado = ESTADO_EN_MANTENIMIENTO;
                    await _db.SaveChangesAsync();
                }
            }

            return NoContent();
        }

        // DELETE: api/ordenestrabajo/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOT(int id)
        {
            var ot = await _db.OrdenesTrabajo.FindAsync(id);
            if (ot == null) return NotFound();

            // PBI-05
            if (ot.Estado == "Cerrada" || ot.Estado == "Anulada")
            {
                return BadRequest("Consulta: Los registros finalizados son inalterables por auditoría.");
            }

            var tieneIntervenciones = await _db.Intervenciones.AnyAsync(i => i.IdOT == id);
            var tieneUsoInsumo = await _db.UsosInsumo.AnyAsync(u => u.IdOT == id);
            if (tieneIntervenciones || tieneUsoInsumo)
                return BadRequest("No se puede eliminar: la OT tiene intervenciones o uso de insumos asociados. Considere anularla en lugar de eliminarla.");

            _db.OrdenesTrabajo.Remove(ot);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}