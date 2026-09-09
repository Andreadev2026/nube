using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.API.DTOs;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HospitalesExternosController : ControllerBase
    {
        private readonly NodusDbContext _db;

        public HospitalesExternosController(NodusDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HospitalExternoDto>>> GetHospitales()
        {
            var hospitales = await _db.HospitalesExternos
                .OrderBy(h => h.Nombre)
                .Select(h => new HospitalExternoDto
                {
                    IdHospital = h.IdHospital,
                    Nombre = h.Nombre,
                    Localidad = h.Localidad,
                    Direccion = h.Direccion,
                    Contacto = h.Contacto,
                    Telefono = h.Telefono,
                    Email = h.Email,
                    Activo = h.Activo
                })
                .ToListAsync();

            return Ok(hospitales);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<HospitalExternoDto>> GetHospital(int id)
        {
            var h = await _db.HospitalesExternos.FindAsync(id);
            if (h == null) return NotFound();

            return Ok(new HospitalExternoDto
            {
                IdHospital = h.IdHospital,
                Nombre = h.Nombre,
                Localidad = h.Localidad,
                Direccion = h.Direccion,
                Contacto = h.Contacto,
                Telefono = h.Telefono,
                Email = h.Email,
                Activo = h.Activo
            });
        }

        [HttpPost]
        public async Task<ActionResult<HospitalExterno>> PostHospital(HospitalExterno hospital)
        {
            if (string.IsNullOrWhiteSpace(hospital.Nombre))
                return BadRequest("El nombre del hospital es obligatorio.");

            hospital.Activo = true;
            _db.HospitalesExternos.Add(hospital);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetHospital), new { id = hospital.IdHospital }, hospital);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutHospital(int id, HospitalExterno hospital)
        {
            if (id != hospital.IdHospital) return BadRequest();
            if (string.IsNullOrWhiteSpace(hospital.Nombre))
                return BadRequest("El nombre del hospital es obligatorio.");

            _db.Entry(hospital).State = EntityState.Modified;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_db.HospitalesExternos.Any(h => h.IdHospital == id)) return NotFound();
                throw;
            }
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHospital(int id)
        {
            var hospital = await _db.HospitalesExternos.FindAsync(id);
            if (hospital == null) return NotFound();

            var tienePrestamos = await _db.Prestamos.AnyAsync(p => p.IdHospital == id);
            if (tienePrestamos)
            {
                // No lo borramos: lo desactivamos para no perder el historial de préstamos
                hospital.Activo = false;
                await _db.SaveChangesAsync();
                return Ok(new { mensaje = "El hospital tiene préstamos registrados. Se desactivó en lugar de eliminarlo." });
            }

            _db.HospitalesExternos.Remove(hospital);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
