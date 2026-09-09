using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IntervencionesController : ControllerBase
    {
        private readonly NodusDbContext _db;

        private static readonly string[] RESULTADOS_VALIDOS = {
            "Aprobado",
            "Aprobado con observaciones",
            "Reprobado",
            "No aplica"
        };

        public IntervencionesController(NodusDbContext db)
        {
            _db = db;
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

            // La OT tiene que existir y no estar anulada
            var ot = await _db.OrdenesTrabajo.FindAsync(i.IdOT);
            if (ot == null) return BadRequest("La OT seleccionada no existe.");
            if (ot.Estado == "Anulada")
                return BadRequest("No se pueden registrar intervenciones en una OT anulada.");

            i.Fecha = DateTime.SpecifyKind(i.Fecha, DateTimeKind.Utc);

            // REGLA DE NEGOCIO: la intervención no puede ser anterior a la apertura de la OT
            if (i.Fecha.Date < ot.FechaApertura.Date)
                return BadRequest($"La fecha de la intervención ({i.Fecha:dd/MM/yyyy}) no puede ser anterior a la apertura de la OT ({ot.FechaApertura:dd/MM/yyyy}).");

            // REGLA DE NEGOCIO: si la OT ya está cerrada, no aceptar intervenciones posteriores al cierre
            if (ot.FechaCierre.HasValue && i.Fecha.Date > ot.FechaCierre.Value.Date)
                return BadRequest($"La fecha de la intervención ({i.Fecha:dd/MM/yyyy}) no puede ser posterior al cierre de la OT ({ot.FechaCierre.Value:dd/MM/yyyy}).");

            // REGLA DE NEGOCIO: si la OT estaba "Pendiente", pasa a "En curso"
            if (ot.Estado == "Pendiente")
            {
                ot.Estado = "En curso";
            }

            _db.Intervenciones.Add(i);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetIntervencion), new { id = i.IdIntervencion }, i);
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

            i.Fecha = DateTime.SpecifyKind(i.Fecha, DateTimeKind.Utc);

            // REGLA: validar coherencia de fechas con la OT (al editar también)
            var ot = await _db.OrdenesTrabajo.AsNoTracking().FirstOrDefaultAsync(o => o.IdOT == i.IdOT);
            if (ot == null) return BadRequest("La OT asociada no existe.");
            if (i.Fecha.Date < ot.FechaApertura.Date)
                return BadRequest($"La fecha de la intervención ({i.Fecha:dd/MM/yyyy}) no puede ser anterior a la apertura de la OT ({ot.FechaApertura:dd/MM/yyyy}).");
            if (ot.FechaCierre.HasValue && i.Fecha.Date > ot.FechaCierre.Value.Date)
                return BadRequest($"La fecha de la intervención ({i.Fecha:dd/MM/yyyy}) no puede ser posterior al cierre de la OT ({ot.FechaCierre.Value:dd/MM/yyyy}).");

            _db.Entry(i).State = EntityState.Modified;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _db.Intervenciones.AnyAsync(x => x.IdIntervencion == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/intervenciones/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteIntervencion(int id)
        {
            var i = await _db.Intervenciones.FindAsync(id);
            if (i == null) return NotFound();

            _db.Intervenciones.Remove(i);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}