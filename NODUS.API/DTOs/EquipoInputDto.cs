namespace NODUS.API.DTOs
{
    // DTO que recibe el body de POST /equipos y PUT /equipos/{id}
    // Reemplaza al binding directo sobre la entidad Equipo, para poder
    // aceptar también la lista de componentes marcados en el formulario.
    public class EquipoInputDto
    {
        public int IdEquipo { get; set; } // solo se usa en PUT
        public string Modelo { get; set; } = null!;
        public string NroSerie { get; set; } = null!;
        public DateTime FechaAdquisicion { get; set; }
        public DateTime? VencimientoGarantia { get; set; }
        public int IdMarca { get; set; }
        public int IdTipo { get; set; }
        public int IdEstado { get; set; }

        // Nombres de los componentes tildados en el checklist del frontend
        public List<string> Componentes { get; set; } = new();
    }
}