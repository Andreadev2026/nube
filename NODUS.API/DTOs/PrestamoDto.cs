namespace NODUS.API.DTOs
{
    public class PrestamoDto
    {
        public int IdPrestamo { get; set; }
        public DateTime FechaPrestamo { get; set; }
        public DateTime? FechaEstimadaDevolucion { get; set; }
        public DateTime? FechaDevolucionReal { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Motivo { get; set; }
        public string? Observaciones { get; set; }

        public int IdEquipo { get; set; }
        public int IdHospital { get; set; }
        public int IdUsuario { get; set; }

        public EquipoResumenDto? Equipo { get; set; }
        public HospitalExternoDto? Hospital { get; set; }
        public UsuarioResumenDto? Usuario { get; set; }

        // Nombres de los componentes que se prestaron junto con el equipo
        public List<string> Componentes { get; set; } = new();
    }

    public class EquipoResumenDto
    {
        public int IdEquipo { get; set; }
        public string Modelo { get; set; } = string.Empty;
        public string NroSerie { get; set; } = string.Empty;
    }

    public class UsuarioResumenDto
    {
        public int IdUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }
}