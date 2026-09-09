namespace NODUS.API.DTOs
{
    // Body de PUT /api/prestamos/{id}/devolucion
    public class DevolucionInputDto
    {
        public DateTime FechaDevolucionReal { get; set; }
        public string? Observaciones { get; set; }
    }
}

