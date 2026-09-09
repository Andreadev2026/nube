using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InsumosController : ControllerBase
    {
        private readonly NodusDbContext _db;

        public InsumosController(NodusDbContext db)
        {
            _db = db;
        }

        // GET: api/insumos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Insumo>>> GetInsumos()
        {
            var insumos = await _db.Insumos
                .OrderBy(i => i.Nombre)
                .ToListAsync();
            return Ok(insumos);
        }

        // GET: api/insumos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Insumo>> GetInsumo(int id)
        {
            var insumo = await _db.Insumos.FindAsync(id);
            if (insumo == null) return NotFound();
            return Ok(insumo);
        }

        // GET: api/insumos/criticos
        // Devuelve insumos con stock por debajo del mínimo (para alertas/dashboard)
        [HttpGet("criticos")]
        public async Task<ActionResult<IEnumerable<Insumo>>> GetCriticos()
        {
            var criticos = await _db.Insumos
                .Where(i => i.StockActual < i.StockMinimo)
                .OrderBy(i => i.StockActual)
                .ToListAsync();
            return Ok(criticos);
        }

        // POST: api/insumos
        [HttpPost]
        public async Task<ActionResult<Insumo>> PostInsumo(Insumo insumo)
        {
            if (string.IsNullOrWhiteSpace(insumo.Nombre))
                return BadRequest("El nombre del insumo es obligatorio.");
            if (string.IsNullOrWhiteSpace(insumo.Tipo))
                return BadRequest("El tipo de insumo es obligatorio.");
            if (insumo.StockActual < 0 || insumo.StockMinimo < 0)
                return BadRequest("Los valores de stock no pueden ser negativos.");

            // Evitar nombres duplicados (case-insensitive)
            var existe = await _db.Insumos.AnyAsync(i =>
                i.Nombre.ToLower() == insumo.Nombre.ToLower());
            if (existe)
                return BadRequest("Ya existe un insumo con ese nombre.");

            _db.Insumos.Add(insumo);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInsumo), new { id = insumo.IdInsumo }, insumo);
        }

        // PUT: api/insumos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInsumo(int id, Insumo insumo)
        {
            if (id != insumo.IdInsumo) return BadRequest();
            if (insumo.StockActual < 0 || insumo.StockMinimo < 0)
                return BadRequest("Los valores de stock no pueden ser negativos.");

            _db.Entry(insumo).State = EntityState.Modified;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _db.Insumos.AnyAsync(i => i.IdInsumo == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/insumos/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInsumo(int id)
        {
            var insumo = await _db.Insumos.FindAsync(id);
            if (insumo == null) return NotFound();

            // No permitir borrar si está usado en algún UsoInsumo (historial)
            var enUso = await _db.UsosInsumo.AnyAsync(u => u.IdInsumo == id);
            if (enUso)
                return BadRequest("No se puede eliminar: el insumo está referenciado en órdenes de trabajo. Considere ajustar su stock a 0 en lugar de eliminarlo.");

            _db.Insumos.Remove(insumo);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}