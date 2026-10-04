namespace NODUS.Models
{
    // Historial de extensiones de la fecha estimada de devolución de un préstamo
    public class ProrrogaPrestamo
    {
        public int IdProrroga { get; set; }

        public int IdPrestamo { get; set; }
        public Prestamo? Prestamo { get; set; }

        public DateTime? FechaAnterior { get; set; } // null si el préstamo no tenía fecha estimada
        public DateTime FechaNueva { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public DateTime FechaSolicitud { get; set; }

        public int IdUsuario { get; set; } // quien autorizó la prórroga
        public Usuario? Usuario { get; set; }
    }
}
