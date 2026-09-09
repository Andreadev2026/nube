using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BajasPatrimonialesController : ControllerBase
    {
        private readonly NodusDbContext _db;

        // IDs de estados del equipo (matchean con el seed del DbContext)
        private const int ESTADO_EN_SERVICIO = 1;
        private const int ESTADO_BAJA_PATRIMONIAL = 5;

        private static readonly string[] MOTIVOS_VALIDOS = {
            "Obsolescencia tecnológica",
            "Falla irreparable",
            "Vida útil agotada",
            "Daño físico irreversible",
            "Robo / extravío",
            "Decisión administrativa"
        };

        public BajasPatrimonialesController(NodusDbContext db)
        {
            _db = db;
        }

        // GET: api/bajaspatrimoniales
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BajaPatrimonial>>> GetBajas()
        {
            var bajas = await _db.BajasPatrimoniales
                .Include(b => b.Equipo).ThenInclude(e => e!.Marca)
                .Include(b => b.Equipo).ThenInclude(e => e!.TipoActivo)
                .Include(b => b.Usuario)
                .OrderByDescending(b => b.Fecha)
                .ToListAsync();
            return Ok(bajas);
        }

        // GET: api/bajaspatrimoniales/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BajaPatrimonial>> GetBaja(int id)
        {
            var b = await _db.BajasPatrimoniales
                .Include(x => x.Equipo).ThenInclude(e => e!.Marca)
                .Include(x => x.Equipo).ThenInclude(e => e!.TipoActivo)
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.IdBaja == id);

            if (b == null) return NotFound();
            return Ok(b);
        }

        // POST: api/bajaspatrimoniales
        [HttpPost]
        public async Task<ActionResult<BajaPatrimonial>> PostBaja(BajaPatrimonial baja)
        {
            // Validaciones de campos obligatorios
            if (string.IsNullOrWhiteSpace(baja.Motivo))
                return BadRequest("El motivo de la baja es obligatorio.");
            if (!MOTIVOS_VALIDOS.Contains(baja.Motivo))
                return BadRequest($"Motivo inválido. Valores permitidos: {string.Join(", ", MOTIVOS_VALIDOS)}.");
            if (string.IsNullOrWhiteSpace(baja.InformeObsolescencia))
                return BadRequest("El informe técnico es obligatorio para dar de baja un equipo.");

            // El equipo tiene que existir
            var equipo = await _db.Equipos.FindAsync(baja.IdEquipo);
            if (equipo == null) return BadRequest("El equipo seleccionado no existe.");

            // REGLA: un equipo no puede tener dos bajas
            var yaTieneBaja = await _db.BajasPatrimoniales.AnyAsync(b => b.IdEquipo == baja.IdEquipo);
            if (yaTieneBaja)
                return BadRequest("Este equipo ya tiene una baja patrimonial registrada. No se puede dar de baja dos veces.");

            // REGLA: no permitir baja si hay OTs abiertas
            var tieneOtsAbiertas = await _db.OrdenesTrabajo.AnyAsync(o =>
                o.IdEquipo == baja.IdEquipo &&
                (o.Estado == "Pendiente" || o.Estado == "En curso"));
            if (tieneOtsAbiertas)
                return BadRequest("No se puede dar de baja: el equipo tiene órdenes de trabajo pendientes o en curso. Cerralas o anulalas antes de proceder.");

            baja.Fecha = DateTime.SpecifyKind(baja.Fecha, DateTimeKind.Utc);

            // REGLA: cambiar automáticamente el estado del equipo a "Baja patrimonial"
            equipo.IdEstado = ESTADO_BAJA_PATRIMONIAL;

            _db.BajasPatrimoniales.Add(baja);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBaja), new { id = baja.IdBaja }, baja);
        }

        // PUT: api/bajaspatrimoniales/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBaja(int id, BajaPatrimonial baja)
        {
            if (id != baja.IdBaja) return BadRequest();
            if (!MOTIVOS_VALIDOS.Contains(baja.Motivo))
                return BadRequest("Motivo inválido.");
            if (string.IsNullOrWhiteSpace(baja.InformeObsolescencia))
                return BadRequest("El informe técnico es obligatorio.");

            baja.Fecha = DateTime.SpecifyKind(baja.Fecha, DateTimeKind.Utc);
            _db.Entry(baja).State = EntityState.Modified;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _db.BajasPatrimoniales.AnyAsync(b => b.IdBaja == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/bajaspatrimoniales/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBaja(int id)
        {
            var baja = await _db.BajasPatrimoniales.FindAsync(id);
            if (baja == null) return NotFound();

            // REGLA: al eliminar la baja, devolver el equipo a "En servicio"
            // (esto sirve si la baja se hizo por error y hay que revertirla)
            var equipo = await _db.Equipos.FindAsync(baja.IdEquipo);
            if (equipo != null && equipo.IdEstado == ESTADO_BAJA_PATRIMONIAL)
            {
                equipo.IdEstado = ESTADO_EN_SERVICIO;
            }

            _db.BajasPatrimoniales.Remove(baja);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}