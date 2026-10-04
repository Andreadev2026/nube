namespace NODUS.Models
{
    public class Prestamo
    {
        public int IdPrestamo { get; set; }
        public DateTime FechaPrestamo { get; set; }
        public DateTime? FechaEstimadaDevolucion { get; set; }
        // Fecha pactada al crear el préstamo; no cambia con las prórrogas (FechaEstimadaDevolucion sí)
        public DateTime? FechaEstimadaDevolucionOriginal { get; set; }
        public DateTime? FechaDevolucionReal { get; set; }
        public string Estado { get; set; } = "En préstamo"; // "En préstamo" | "Devuelto"
        public string? Motivo { get; set; }
        public string? Observaciones { get; set; }

        // Remito de salida: número correlativo por año (REM-2026-0001) y ruta del PDF relativa al almacenamiento
        public string NumeroRemito { get; set; } = string.Empty;
        public string? RutaRemitoPdf { get; set; }
        // Se fija la primera vez que se genera el PDF; si se regenera, conserva la fecha original
        public DateTime? FechaEmisionRemito { get; set; }
        // Escaneo del remito de salida firmado por quien retira el equipo (opcional, se sube cuando esté)
        public string? RutaRemitoSalidaFirmado { get; set; }
        public DateTime? FechaCargaFirmaSalida { get; set; }

        // Remito de devolución (DEV): se genera al registrar la devolución, con número correlativo propio
        public string? NumeroRemitoDevolucion { get; set; }
        public string? RutaRemitoDevolucionPdf { get; set; }
        // Estado del equipo al volver: se imprime en el remito DEV (la fecha es FechaDevolucionReal)
        public string? ObservacionesDevolucion { get; set; }
        public string? RutaRemitoDevolucionFirmado { get; set; }
        public DateTime? FechaCargaFirmaDevolucion { get; set; }

        public int IdEquipo { get; set; }
        public Equipo? Equipo { get; set; }

        public int IdHospital { get; set; }
        public HospitalExterno? Hospital { get; set; }

        public int IdUsuario { get; set; }
        public Usuario? Usuario { get; set; }

        public ICollection<PrestamoComponente> PrestamoComponentes { get; set; } = new List<PrestamoComponente>();
        public ICollection<ProrrogaPrestamo> Prorrogas { get; set; } = new List<ProrrogaPrestamo>();
    }
}