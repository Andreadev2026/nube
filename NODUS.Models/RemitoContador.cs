namespace NODUS.Models
{
    // Último número de remito emitido por tipo ("REM" salida, "DEV" devolución) y año.
    // Se incrementa dentro de la misma transacción que usa el número, así la numeración no se repite ni se saltea.
    public class RemitoContador
    {
        public string Tipo { get; set; } = "REM";
        public int Anio { get; set; }
        public int UltimoNumero { get; set; }
    }
}
