namespace NODUS.API.DTOs
{
    // Registro de la devolución: cierra el préstamo y genera el remito DEV con estos datos impresos.
    // El escaneo firmado del DEV se sube después, aparte (POST /api/remitos/{id}/subir-firma-devolucion).
    public class RemitoDevolucionInputDto
    {
        public DateTime FechaDevolucion { get; set; }
        public string? Observaciones { get; set; }
    }
}
