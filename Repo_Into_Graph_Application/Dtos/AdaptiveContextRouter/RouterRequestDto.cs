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
        /// Ngon ngu cua SourceCode: "csharp" hoac "java" (cung chap nhan "c#", "dotnet",
        /// hoac ten hien thi cua parser nhu "Java (Spring Boot)" — xem LanguageNormalizer).
        ///
        /// MAC DINH LA RONG, KHONG PHAI "csharp": khi client khong khai bao, router se suy
        /// luan tu chinh ma nguon. Mac dinh "csharp" truoc day khien moi request Java khong
        /// khai bao ngon ngu bi phan tich bang tree-sitter C# ma khong bao loi — V(G) va SLOC
        /// sai dan den dinh tuyen sai, rat kho phat hien.
        /// </summary>
        public string Language { get; set; } = string.Empty;
    }
}
