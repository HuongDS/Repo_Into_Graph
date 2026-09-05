namespace Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter
{
    public class RouterRequestDto
    {
        /// <summary>
        /// ID dinh danh module. Do client cung cap de tracking qua cac tang.
        /// Vd: "MOD_001", "AppointmentService.createAppointment"
        /// </summary>
        public string ModuleId { get; set; } = string.Empty;

        public string SourceCode { get; set; } = string.Empty;
        
        /// <summary>
        /// e.g. "csharp", "java"
        /// </summary>
        public string Language { get; set; } = "csharp";
    }
}
