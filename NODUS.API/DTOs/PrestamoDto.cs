namespace NODUS.API.DTOs
{
    public class PrestamoDto
    {
        public int IdPrestamo { get; set; }
        public DateTime FechaPrestamo { get; set; }
        public DateTime? FechaEstimadaDevolucion { get; set; }
        public DateTime? FechaEstimadaDevolucionOriginal { get; set; }
        public DateTime? FechaDevolucionReal { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Motivo { get; set; }
        public string? Observaciones { get; set; }

        public string NumeroRemito { get; set; } = string.Empty;
        public bool TieneRemitoPdf { get; set; }
        public DateTime? FechaEmisionRemito { get; set; }
        public bool TieneFirmaSalida { get; set; }
        public DateTime? FechaCargaFirmaSalida { get; set; }

        // Remito de devolución: existe desde que se registra la devolución
        public string? NumeroRemitoDevolucion { get; set; }
        public string? ObservacionesDevolucion { get; set; }
        public bool TieneFirmaDevolucion { get; set; }
        public DateTime? FechaCargaFirmaDevolucion { get; set; }

        public int IdEquipo { get; set; }
        public int IdHospital { get; set; }
        public int IdUsuario { get; set; }

        public EquipoResumenDto? Equipo { get; set; }
        public HospitalExternoDto? Hospital { get; set; }
        public UsuarioResumenDto? Usuario { get; set; }

        // Nombres de los componentes que se prestaron junto con el equipo
        public List<string> Componentes { get; set; } = new();

        // Historial de prórrogas, de la más antigua a la más reciente
        public List<ProrrogaDto> Prorrogas { get; set; } = new();
    }

    public class ProrrogaDto
    {
        public int IdProrroga { get; set; }
        public DateTime? FechaAnterior { get; set; }
        public DateTime FechaNueva { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public DateTime FechaSolicitud { get; set; }
        public string? AutorizadoPor { get; set; }
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

    // Préstamo activo con lo necesario para ubicarlo en el mapa
    public class PrestamoMapaDto
    {
        public int IdPrestamo { get; set; }
        public DateTime FechaPrestamo { get; set; }
        public DateTime? FechaEstimadaDevolucion { get; set; }
        public string? Motivo { get; set; }

        public int IdEquipo { get; set; }
        public string Modelo { get; set; } = string.Empty;
        public string NroSerie { get; set; } = string.Empty;
        public string? Marca { get; set; }
        public string? TipoActivo { get; set; }
        public List<string> Componentes { get; set; } = new();

        public int IdHospital { get; set; }
        public string Hospital { get; set; } = string.Empty;
        public string? Direccion { get; set; }
        public string? Localidad { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }

        // Contacto del hospital receptor y usuario que registró el préstamo
        public string? Contacto { get; set; }
        public string? Telefono { get; set; }
        public string? Responsable { get; set; }
    }
}