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

        /// <summary>
        /// Khi true, bo qua nguong SLOC/Cyclomatic Complexity (SLOC &lt; 25 hoac Vg &lt;= 2)
        /// va LUON dinh tuyen sang HybridGraph.
        ///
        /// Nguong RawCode duoc thiet ke cho quyet dinh cua Tang 1/2 khi sinh prompt cho LLM
        /// (ham qua don gian thi khong can ton cong build CFG). Nhung tinh nang "View Graph"
        /// o GUI Tang 3 can XEM duoc do thi cua 1 function bat ke ham do don gian den muc nao,
        /// nen phai co co che bypass rieng thay vi dung chung nguong nay.
        /// </summary>
        public bool ForceHybrid { get; set; } = false;
    }
}
