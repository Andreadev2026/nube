namespace NODUS.Models
{
    public class Ubicacion
    {
        public int IdUbicacion { get; set; }
        public DateTime FechaTraslado { get; set; }

        // FK al sector de origen (antes era string libre)
        public int IdSectorAnterior { get; set; }
        public Sector? SectorAnterior { get; set; }

        // FK al sector destino (antes era string libre)
        public int IdSectorNuevo { get; set; }
        public Sector? SectorNuevo { get; set; }

        public int IdEquipo { get; set; }
        public Equipo? Equipo { get; set; }

        public int IdServicio { get; set; }
        public Servicio? Servicio { get; set; }

        public int IdUsuario { get; set; }
        public Usuario? Usuario { get; set; }
    }
}