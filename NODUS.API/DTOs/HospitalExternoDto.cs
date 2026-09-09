namespace NODUS.API.DTOs
{
    public class HospitalExternoDto
    {
        public int IdHospital { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Localidad { get; set; }
        public string? Direccion { get; set; }
        public string? Contacto { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public bool Activo { get; set; }
    }
}