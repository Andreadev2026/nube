namespace NODUS.API.DTOs
{
    // Ficha de solo lectura que se abre al escanear el QR. Solo datos que puede ver cualquier usuario:
    // nada de costos, proveedor ni datos internos.
    public class FichaQrDto
    {
        public int IdEquipo { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string NroSerie { get; set; } = string.Empty;
        public string? Estado { get; set; }
        public string? ColorEstado { get; set; }
        public string? Marca { get; set; }
        public string Modelo { get; set; } = string.Empty;
        public string? TipoActivo { get; set; }
        public string? Ubicacion { get; set; }
        public string? Origen { get; set; }
        public DateTime FechaAlta { get; set; }
        // Tipos de las alertas activas (No leída / Leída) del equipo. Vacía = ninguna
        public List<string> Alertas { get; set; } = new();
        public List<string> Componentes { get; set; } = new();
        public bool ReportadoHoy { get; set; }
        public int IdEstado { get; set; }
        // Solo los equipos "En servicio" admiten reportes de falla; si no, el motivo para mostrar en la ficha
        public bool PuedeReportar { get; set; }
        public string? MotivoNoReportable { get; set; }
    }

    public class ReporteFallaInputDto
    {
        public string? NombreCompleto { get; set; }
        public string? Legajo { get; set; }
        public string? Descripcion { get; set; }
        // Servicio desde el que se reporta (obligatorio)
        public int? ServicioId { get; set; }
    }

    // Opción del catálogo de servicios para el formulario de reporte
    public class ServicioCatalogoDto
    {
        public int IdServicio { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class ConvertirEnOTInputDto
    {
        // Servicio de la OT. Si no viene, se usa el del último traslado del equipo
        public int? IdServicio { get; set; }
    }

    public class ReporteFallaDto
    {
        public int IdReporteFalla { get; set; }
        public int IdEquipo { get; set; }
        public string? Equipo { get; set; }
        public string? NroSerie { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Legajo { get; set; } = string.Empty;
        public string? Usuario { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string? Servicio { get; set; }
        public DateTime FechaReporte { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int? IdOT { get; set; }
        public int? IdServicioSugerido { get; set; }
    }
}
