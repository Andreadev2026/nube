using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NODUS.API.DTOs;
using NODUS.Data;
using NODUS.Models;

namespace NODUS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PrestamosController : ControllerBase
    {
        private readonly NodusDbContext _db;

        // IDs de estado_equipo ya existentes en el seed (ver NodusDbContext)
        private const int ID_ESTADO_EN_SERVICIO = 1;
        private const int ID_ESTADO_EN_PRESTAMO = 4;

        public PrestamosController(NodusDbContext db)
        {
            _db = db;
        }

        private static PrestamoDto ToDto(Prestamo p) => new PrestamoDto
        {
            IdPrestamo = p.IdPrestamo,
            FechaPrestamo = p.FechaPrestamo,
            FechaEstimadaDevolucion = p.FechaEstimadaDevolucion,
            FechaDevolucionReal = p.FechaDevolucionReal,
            Estado = p.Estado,
            Motivo = p.Motivo,
            Observaciones = p.Observaciones,
            IdEquipo = p.IdEquipo,
            IdHospital = p.IdHospital,
            IdUsuario = p.IdUsuario,
            Equipo = p.Equipo == null ? null : new EquipoResumenDto
            {
                IdEquipo = p.Equipo.IdEquipo,
                Modelo = p.Equipo.Modelo,
                NroSerie = p.Equipo.NroSerie
            },
            Hospital = p.Hospital == null ? null : new HospitalExternoDto
            {
                IdHospital = p.Hospital.IdHospital,
                Nombre = p.Hospital.Nombre,
                Localidad = p.Hospital.Localidad,
                Direccion = p.Hospital.Direccion,
                Contacto = p.Hospital.Contacto,
                Telefono = p.Hospital.Telefono,
                Email = p.Hospital.Email,
                Latitud = p.Hospital.Latitud,
                Longitud = p.Hospital.Longitud,
                Activo = p.Hospital.Activo
            },
            Usuario = p.Usuario == null ? null : new UsuarioResumenDto
            {
                IdUsuario = p.Usuario.IdUsuario,
                Nombre = p.Usuario.Nombre
            },
            Componentes = p.PrestamoComponentes.Select(c => c.Componente).ToList()
        };

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PrestamoDto>>> GetPrestamos()
        {
            var prestamos = await _db.Prestamos
                .Include(p => p.Equipo)
                .Include(p => p.Hospital)
                .Include(p => p.Usuario)
                .Include(p => p.PrestamoComponentes)
                .OrderByDescending(p => p.FechaPrestamo)
                .ToListAsync();

            return Ok(prestamos.Select(ToDto));
        }

        [HttpGet("mapa")]
        public async Task<ActionResult<IEnumerable<PrestamoMapaDto>>> GetMapa()
        {
            var activos = await _db.Prestamos
                .Where(p => p.FechaDevolucionReal == null && p.Estado != "Devuelto")
                .OrderBy(p => p.FechaPrestamo)
                .Select(p => new PrestamoMapaDto
                {
                    IdPrestamo = p.IdPrestamo,
                    FechaPrestamo = p.FechaPrestamo,
                    FechaEstimadaDevolucion = p.FechaEstimadaDevolucion,
                    Motivo = p.Motivo,
                    IdEquipo = p.IdEquipo,
                    Modelo = p.Equipo!.Modelo,
                    NroSerie = p.Equipo.NroSerie,
                    Marca = p.Equipo.Marca!.Nombre,
                    TipoActivo = p.Equipo.TipoActivo!.Nombre,
                    Componentes = p.PrestamoComponentes.Select(c => c.Componente).ToList(),
                    IdHospital = p.IdHospital,
                    Hospital = p.Hospital!.Nombre,
                    Direccion = p.Hospital.Direccion,
                    Localidad = p.Hospital.Localidad,
                    Latitud = p.Hospital.Latitud,
                    Longitud = p.Hospital.Longitud,
                    Contacto = p.Hospital.Contacto,
                    Telefono = p.Hospital.Telefono,
                    Responsable = p.Usuario!.Nombre
                })
                .ToListAsync();

            return Ok(activos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PrestamoDto>> GetPrestamo(int id)
        {
            var p = await _db.Prestamos
                .Include(x => x.Equipo)
                .Include(x => x.Hospital)
                .Include(x => x.Usuario)
                .Include(x => x.PrestamoComponentes)
                .FirstOrDefaultAsync(x => x.IdPrestamo == id);

            if (p == null) return NotFound();
            return Ok(ToDto(p));
        }

        [HttpPost]
        public async Task<ActionResult<PrestamoDto>> PostPrestamo(PrestamoInputDto input)
        {
            var equipo = await _db.Equipos.FindAsync(input.IdEquipo);
            if (equipo == null) return BadRequest("El equipo indicado no existe.");

            if (equipo.IdEstado != ID_ESTADO_EN_SERVICIO)
                return BadRequest("El equipo no está en servicio, no se puede prestar en su estado actual.");

            var hospital = await _db.HospitalesExternos.FindAsync(input.IdHospital);
            if (hospital == null) return BadRequest("El hospital indicado no existe.");

            if (input.FechaEstimadaDevolucion.HasValue && input.FechaEstimadaDevolucion.Value < input.FechaPrestamo)
                return BadRequest("La fecha estimada de devolución no puede ser anterior a la fecha de préstamo.");

            var prestamo = new Prestamo
            {
                IdEquipo = input.IdEquipo,
                IdHospital = input.IdHospital,
                IdUsuario = input.IdUsuario,
                FechaPrestamo = DateTime.SpecifyKind(input.FechaPrestamo, DateTimeKind.Utc),
                FechaEstimadaDevolucion = input.FechaEstimadaDevolucion.HasValue
                    ? DateTime.SpecifyKind(input.FechaEstimadaDevolucion.Value, DateTimeKind.Utc)
                    : null,
                Observaciones = input.Observaciones,
                Motivo = input.Motivo,
                Estado = "En préstamo",
                PrestamoComponentes = (input.Componentes ?? new List<string>())
                    .Select(nombre => new PrestamoComponente { Componente = nombre })
                    .ToList()
            };

            _db.Prestamos.Add(prestamo);

            // El equipo pasa a "En préstamo" mientras dure el préstamo
            equipo.IdEstado = ID_ESTADO_EN_PRESTAMO;

            await _db.SaveChangesAsync();

            var creado = await _db.Prestamos
                .Include(x => x.Equipo)
                .Include(x => x.Hospital)
                .Include(x => x.Usuario)
                .Include(x => x.PrestamoComponentes)
                .FirstAsync(x => x.IdPrestamo == prestamo.IdPrestamo);

            return CreatedAtAction(nameof(GetPrestamo), new { id = prestamo.IdPrestamo }, ToDto(creado));
        }

        [HttpPut("{id}/devolucion")]
        public async Task<IActionResult> RegistrarDevolucion(int id, DevolucionInputDto input)
        {
            var prestamo = await _db.Prestamos
                .Include(p => p.Equipo)
                .FirstOrDefaultAsync(p => p.IdPrestamo == id);

            if (prestamo == null) return NotFound();
            if (prestamo.Estado == "Devuelto")
                return BadRequest("Este préstamo ya fue registrado como devuelto.");

            prestamo.FechaDevolucionReal = DateTime.SpecifyKind(input.FechaDevolucionReal, DateTimeKind.Utc);
            prestamo.Estado = "Devuelto";
            if (!string.IsNullOrWhiteSpace(input.Observaciones))
            {
                prestamo.Observaciones = string.IsNullOrWhiteSpace(prestamo.Observaciones)
                    ? input.Observaciones
                    : $"{prestamo.Observaciones}\n---\nDevolución: {input.Observaciones}";
            }

            // El equipo vuelve a estar en servicio
            if (prestamo.Equipo != null)
                prestamo.Equipo.IdEstado = ID_ESTADO_EN_SERVICIO;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePrestamo(int id)
        {
            var prestamo = await _db.Prestamos.Include(p => p.Equipo).FirstOrDefaultAsync(p => p.IdPrestamo == id);
            if (prestamo == null) return NotFound();

            // Si se borra un préstamo activo por error, el equipo vuelve a estar en servicio
            if (prestamo.Estado == "En préstamo" && prestamo.Equipo != null)
                prestamo.Equipo.IdEstado = ID_ESTADO_EN_SERVICIO;

            _db.Prestamos.Remove(prestamo);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}