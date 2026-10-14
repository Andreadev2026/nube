namespace NODUS.Models
{
    // Falla reportada desde la ficha QR del equipo. No cambia el estado del equipo:
    // mantenimiento la convierte en OT (eso sí lo cambia) o la descarta.
    public class ReporteFalla
    {
        public const string ESTADO_PENDIENTE = "Pendiente";
        public const string ESTADO_CONVERTIDO = "Convertido en OT";
        public const string ESTADO_DESCARTADO = "Descartado";

        // Índice único parcial (id_equipo, fecha_dia) WHERE estado <> 'Descartado'
        public const string INDICE_UNO_POR_DIA = "ux_reporte_falla_equipo_dia";

        public int IdReporteFalla { get; set; }

        public int IdEquipo { get; set; }
        public Equipo? Equipo { get; set; }

        // Usuario logueado que envió el reporte (sale del token, nunca del body)
        public int IdUsuario { get; set; }
        public Usuario? Usuario { get; set; }

        // Quién reporta realmente (el usuario puede ser genérico)
        public string NombreCompleto { get; set; } = string.Empty;
        public string Legajo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;

        // Servicio desde el que se reporta. Obligatorio al crear (lo valida ReportesFallaService);
        // nullable en la base solo por los reportes anteriores a este campo.
        public int? IdServicio { get; set; }
        public Servicio? Servicio { get; set; }

        public DateTime FechaReporte { get; set; }  // UTC
        public DateOnly FechaDia { get; set; }      // día en Argentina, lo calcula el servidor
        public string Estado { get; set; } = ESTADO_PENDIENTE;

        public int? IdOT { get; set; }
        public OrdenTrabajo? OrdenTrabajo { get; set; }
    }
}
