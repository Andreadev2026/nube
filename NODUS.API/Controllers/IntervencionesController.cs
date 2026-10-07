using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.API.Services;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IntervencionesController : ControllerBase
    {
        private readonly NodusDbContext _db;
        private readonly EstadosOTService _estados;

        private static readonly string[] RESULTADOS_VALIDOS = {
            "Aprobado",
            "Aprobado con observaciones",
            "Reprobado",
            "No aplica"
        };

        private const string MSJ_OT_FINALIZADA =
            "No se pueden registrar ni modificar tareas en una OT cerrada o anulada.";

        public IntervencionesController(NodusDbContext db, EstadosOTService estados)
        {
            _db = db;
            _estados = estados;
        }

        // GET: api/intervenciones
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Intervencion>>> GetIntervenciones()
        {
            var intervenciones = await _db.Intervenciones
                .Include(i => i.OrdenTrabajo).ThenInclude(o => o!.Equipo)
                .Include(i => i.Usuario)
                .OrderByDescending(i => i.Fecha)
                .ToListAsync();
            return Ok(intervenciones);
        }

        // GET: api/intervenciones/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Intervencion>> GetIntervencion(int id)
        {
            var i = await _db.Intervenciones
                .Include(x => x.OrdenTrabajo).ThenInclude(o => o!.Equipo)
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.IdIntervencion == id);

            if (i == null) return NotFound();
            return Ok(i);
        }

        // GET: api/intervenciones/ot/5
        [HttpGet("ot/{idOT}")]
        public async Task<ActionResult<IEnumerable<Intervencion>>> GetPorOT(int idOT)
        {
            var lista = await _db.Intervenciones
                .Include(i => i.Usuario)
                .Where(i => i.IdOT == idOT)
                .OrderBy(i => i.Fecha)
                .ToListAsync();
            return Ok(lista);
        }

        // POST: api/intervenciones
        [HttpPost]
        public async Task<ActionResult<Intervencion>> PostIntervencion(Intervencion i)
        {
            if (string.IsNullOrWhiteSpace(i.TareasRealizadas))
                return BadRequest("Las tareas realizadas son obligatorias.");
            if (i.TiempoEmpleado <= 0)
                return BadRequest("El tiempo empleado debe ser mayor a 0 minutos.");
            if (!RESULTADOS_VALIDOS.Contains(i.ResultadoSegElectrica))
                return BadRequest($"Resultado inválido. Valores permitidos: {string.Join(", ", RESULTADOS_VALIDOS)}.");

            // La OT tiene que existir y seguir abierta
            var ot = await _db.OrdenesTrabajo.AsNoTracking().FirstOrDefaultAsync(o => o.IdOT == i.IdOT);
            if (ot == null) return BadRequest("La OT seleccionada no existe.");
            if (!EstadosOTService.EsAbierta(ot.Estado)) return BadRequest(MSJ_OT_FINALIZADA);

            NormalizarFechas(i);
            var error = ValidarFechas(i, ot);
            if (error != null) return BadRequest(error);

            // La tarea, el estado de la OT y el del equipo se guardan juntos
            await using var tx = await _db.Database.BeginTransactionAsync();
            _db.Intervenciones.Add(i);
            await _db.SaveChangesAsync();
            var aviso = await _estados.RecalcularEstadoOT(i.IdOT);
            await tx.CommitAsync();

            return CreatedAtAction(nameof(GetIntervencion), new { id = i.IdIntervencion }, new { intervencion = i, mensaje = aviso });
        }

        // PUT: api/intervenciones/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutIntervencion(int id, Intervencion i)
        {
            if (id != i.IdIntervencion) return BadRequest();
            if (i.TiempoEmpleado <= 0)
                return BadRequest("El tiempo empleado debe ser mayor a 0 minutos.");
            if (!RESULTADOS_VALIDOS.Contains(i.ResultadoSegElectrica))
                return BadRequest("Resultado inválido.");

            var anterior = await _db.Intervenciones.AsNoTracking().FirstOrDefaultAsync(x => x.IdIntervencion == id);
            if (anterior == null) return NotFound();

            // PBI-05: las tareas de una OT finalizada no se modifican (ni se mueven a una)
            var otAnterior = await _db.OrdenesTrabajo.AsNoTracking().FirstAsync(o => o.IdOT == anterior.IdOT);
            if (!EstadosOTService.EsAbierta(otAnterior.Estado)) return BadRequest(MSJ_OT_FINALIZADA);

            var ot = await _db.OrdenesTrabajo.AsNoTracking().FirstOrDefaultAsync(o => o.IdOT == i.IdOT);
            if (ot == null) return BadRequest("La OT asociada no existe.");
            if (!EstadosOTService.EsAbierta(ot.Estado)) return BadRequest(MSJ_OT_FINALIZADA);

            NormalizarFechas(i);
            var error = ValidarFechas(i, ot);
            if (error != null) return BadRequest(error);

            _db.Entry(i).State = EntityState.Modified;

            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.SaveChangesAsync();
            var aviso = await _estados.RecalcularEstadoOT(i.IdOT);
            if (anterior.IdOT != i.IdOT)
                await _estados.RecalcularEstadoOT(anterior.IdOT);
            await tx.CommitAsync();

            return Ok(new { mensaje = aviso });
        }

        // DELETE: api/intervenciones/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteIntervencion(int id)
        {
            var i = await _db.Intervenciones.FindAsync(id);
            if (i == null) return NotFound();

            var ot = await _db.OrdenesTrabajo.AsNoTracking().FirstAsync(o => o.IdOT == i.IdOT);
            if (!EstadosOTService.EsAbierta(ot.Estado)) return BadRequest(MSJ_OT_FINALIZADA);

            await using var tx = await _db.Database.BeginTransactionAsync();
            _db.Intervenciones.Remove(i);
            await _db.SaveChangesAsync();
            await _estados.RecalcularEstadoOT(i.IdOT);
            await tx.CommitAsync();
            return NoContent();
        }

        private static void NormalizarFechas(Intervencion i)
        {
            i.Fecha = DateTime.SpecifyKind(i.Fecha, DateTimeKind.Utc);
            if (i.FechaFin.HasValue)
                i.FechaFin = DateTime.SpecifyKind(i.FechaFin.Value, DateTimeKind.Utc);
        }

        // REGLAS DE NEGOCIO: la tarea no empieza antes de la apertura de la OT
        // y la fecha de fin no es anterior a la de inicio
        private static string? ValidarFechas(Intervencion i, OrdenTrabajo ot)
        {
            if (i.Fecha.Date < ot.FechaApertura.Date)
                return $"La fecha de inicio de la tarea ({i.Fecha:dd/MM/yyyy}) no puede ser anterior a la apertura de la OT ({ot.FechaApertura:dd/MM/yyyy}).";
            if (i.FechaFin.HasValue && i.FechaFin.Value.Date < i.Fecha.Date)
                return $"La fecha de fin ({i.FechaFin.Value:dd/MM/yyyy}) no puede ser anterior a la fecha de inicio ({i.Fecha:dd/MM/yyyy}).";
            return null;
        }
    }
}