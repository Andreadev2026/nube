namespace NODUS.API.Services
{
    // Roles del sistema (usuario.rol). Tienen que coincidir con permisos.js del frontend.
    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string JefePatrimonio = "Jefe patrimonio";
        public const string TecnicoBiomedico = "Técnico biomédico";
        public const string Medico = "Médico";
        public const string Residente = "Residente";
        public const string Enfermero = "Enfermero";
        public const string JefeServicio = "Jefe servicio";
        public const string AuditorAnmat = "Auditor ANMAT";
        // Usuario genérico de los servicios asistenciales: solo ve la ficha QR y reporta fallas
        public const string PersonalAsistencial = "Personal asistencial";

        public static readonly string[] Todos =
        {
            Administrador, JefePatrimonio, TecnicoBiomedico, Medico, Residente,
            Enfermero, JefeServicio, AuditorAnmat, PersonalAsistencial
        };

        // Para [Authorize(Roles = ...)]: los que gestionan los reportes de falla
        public const string GestionReportesFalla = Administrador + "," + JefePatrimonio + "," + JefeServicio;
    }

    public static class Politicas
    {
        // Política por defecto de toda la API: usuario autenticado que no sea Personal asistencial.
        // Personal asistencial solo entra a los endpoints marcados con [Authorize(Policy = CualquierUsuario)].
        public const string UsuarioInterno = "UsuarioInterno";
        public const string CualquierUsuario = "CualquierUsuario";
    }

    public static class LimitesTasa
    {
        public const string ReportesFalla = "ReportesFalla";
    }
}
