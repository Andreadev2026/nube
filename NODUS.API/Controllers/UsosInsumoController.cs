using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsosInsumoController : ControllerBase
    {
        private readonly NodusDbContext _db;

        public UsosInsumoController(NodusDbContext db)
        {
            _db = db;
        }

        // GET: api/usosinsumo
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UsoInsumo>>> GetUsos()
        {
            var usos = await _db.UsosInsumo
                .Include(u => u.Insumo)
                .Include(u => u.OrdenTrabajo).ThenInclude(o => o!.Equipo)
                .OrderByDescending(u => u.IdUso)
                .ToListAsync();
            return Ok(usos);
        }

        // GET: api/usosinsumo/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UsoInsumo>> GetUso(int id)
        {
            var uso = await _db.UsosInsumo
                .Include(u => u.Insumo)
                .Include(u => u.OrdenTrabajo).ThenInclude(o => o!.Equipo)
                .FirstOrDefaultAsync(u => u.IdUso == id);

            if (uso == null) return NotFound();
            return Ok(uso);
        }

        // GET: api/usosinsumo/ot/5
        // Insumos consumidos por una OT específica
        [HttpGet("ot/{idOT}")]
        public async Task<ActionResult<IEnumerable<UsoInsumo>>> GetPorOT(int idOT)
        {
            var lista = await _db.UsosInsumo
                .Include(u => u.Insumo)
                .Where(u => u.IdOT == idOT)
                .ToListAsync();
            return Ok(lista);
        }

        // POST: api/usosinsumo
        [HttpPost]
        public async Task<ActionResult<UsoInsumo>> PostUso(UsoInsumo uso)
        {
            // Validaciones básicas
            if (uso.Cantidad <= 0)
                return BadRequest("La cantidad debe ser mayor a 0.");

            // La OT tiene que existir y no estar anulada
            var ot = await _db.OrdenesTrabajo.FindAsync(uso.IdOT);
            if (ot == null) return BadRequest("La OT seleccionada no existe.");
            if (ot.Estado == "Anulada")
                return BadRequest("No se pueden registrar insumos consumidos en una OT anulada.");

            // El insumo tiene que existir y tener stock suficiente
            var insumo = await _db.Insumos.FindAsync(uso.IdInsumo);
            if (insumo == null) return BadRequest("El insumo seleccionado no existe.");
            if (insumo.StockActual < uso.Cantidad)
                return BadRequest($"Stock insuficiente. Disponible: {insumo.StockActual}, solicitado: {uso.Cantidad}.");

            // REGLA DE NEGOCIO: descontar el stock del insumo
            insumo.StockActual -= uso.Cantidad;

            _db.UsosInsumo.Add(uso);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUso), new { id = uso.IdUso }, uso);
        }

        // PUT: api/usosinsumo/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUso(int id, UsoInsumo uso)
        {
            if (id != uso.IdUso) return BadRequest();
            if (uso.Cantidad <= 0) return BadRequest("La cantidad debe ser mayor a 0.");

            // Obtener el uso anterior para calcular la diferencia de stock
            var usoAnterior = await _db.UsosInsumo.AsNoTracking().FirstOrDefaultAsync(u => u.IdUso == id);
            if (usoAnterior == null) return NotFound();

            // REGLA DE NEGOCIO: ajustar el stock según la diferencia
            // Caso A: mismo insumo, cantidad distinta → ajustar diferencia
            // Caso B: cambió el insumo → devolver stock al anterior, descontar del nuevo
            if (usoAnterior.IdInsumo == uso.IdInsumo)
            {
                var diferencia = uso.Cantidad - usoAnterior.Cantidad;
                if (diferencia != 0)
                {
                    var insumo = await _db.Insumos.FindAsync(uso.IdInsumo);
                    if (insumo == null) return BadRequest("Insumo no encontrado.");
                    if (insumo.StockActual - diferencia < 0)
                        return BadRequest($"Stock insuficiente para la nueva cantidad. Disponible: {insumo.StockActual}, ajuste requerido: {diferencia}.");
                    insumo.StockActual -= diferencia;
                }
            }
            else
            {
                // Cambió el insumo: devolver stock al anterior y descontar del nuevo
                var insumoAnterior = await _db.Insumos.FindAsync(usoAnterior.IdInsumo);
                var insumoNuevo = await _db.Insumos.FindAsync(uso.IdInsumo);
                if (insumoAnterior == null || insumoNuevo == null)
                    return BadRequest("Insumo no encontrado.");
                if (insumoNuevo.StockActual < uso.Cantidad)
                    return BadRequest($"Stock insuficiente del nuevo insumo. Disponible: {insumoNuevo.StockActual}, solicitado: {uso.Cantidad}.");

                insumoAnterior.StockActual += usoAnterior.Cantidad;
                insumoNuevo.StockActual -= uso.Cantidad;
            }

            _db.Entry(uso).State = EntityState.Modified;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _db.UsosInsumo.AnyAsync(u => u.IdUso == id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/usosinsumo/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUso(int id)
        {
            var uso = await _db.UsosInsumo.FindAsync(id);
            if (uso == null) return NotFound();

            // REGLA DE NEGOCIO: devolver el stock al insumo
            var insumo = await _db.Insumos.FindAsync(uso.IdInsumo);
            if (insumo != null)
            {
                insumo.StockActual += uso.Cantidad;
            }

            _db.UsosInsumo.Remove(uso);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}