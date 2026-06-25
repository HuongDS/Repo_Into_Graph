
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Google.GenAI;
using Google.GenAI.Types;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using FeatureModel = Repo_Into_Graph_DataAccess.Models.Feature.Feature;
using Repo_Into_Graph_DataAccess.Models.FewShot;
using Type = Google.GenAI.Types.Type;
using Repo_Into_Graph_Application.Services.Caculation;




namespace Repo_Into_Graph_Application.Services.AI
{
    public class AIService : IAIService
    {
        private readonly Client _client;

        public AIService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            var apiKey = configuration["GeminiConfig:ApiKey"];

            var clientOptions = new ClientOptions
            {
                HttpClientFactory = () => httpClientFactory.CreateClient()
            };

            _client = new Client(apiKey: apiKey, clientOptions: clientOptions);
        }

        
        public async Task<IEnumerable<GeneratedQuestionDto>> GenerateUnifiedQuestionsAsync(
            string businessName,
            string codeBuilder,
            string contextBuilder,
            int numberOfQuestions,
            string difficulty,
            string? additionalContext = null,
           
            IEnumerable<FewShotExample>? fewShotExamples = null)
        {
            var systemInstruction = $@"Bạn là một Giảng viên đại học chấm thi vấn đáp đồ án phần mềm. Nhiệm vụ của bạn là dựa vào Mã nguồn (Source Code) và Sơ đồ luồng (Mermaid Graph) để tìm ra các Quy tắc nghiệp vụ (Business Rules), sau đó đặt câu hỏi kiểm tra xem sinh viên có hiểu ""Hệ thống này vận hành trên thực tế như thế nào"" hay không.
YÊU CẦU BẮT BUỘC VỀ SỐ LƯỢNG VÀ ĐỘ KHÓ:
- Bạn PHẢI tạo ra chính xác ĐÚNG {numberOfQuestions} câu hỏi. Không được tạo nhiều hơn hoặc ít hơn.
- Tất cả các câu hỏi phải được thiết kế ở mức độ: {difficulty}. (Với mức độ ""Khó"", câu hỏi phải xoay quanh các lỗ hổng logic, kịch bản lỗi, hoặc bài toán đồng bộ dữ liệu giữa các phân hệ).
QUY TẮC ĐẶT CÂU HỎI VÀ TRẢ LỜI (THIẾT QUÂN LUẬT - NGHIÊM CẤM TỪ KHÓA KỸ THUẬT):
1. NGÔN NGỮ THUỒN NGHIỆP VỤ (100% Business Language): Cả câu hỏi (question) và câu trả lời (suggestedAnswer) KHÔNG ĐƯỢC CHỨA bất kỳ từ khóa kỹ thuật, tên framework, hay cấu trúc code nào.
   - CẤM DÙNG: Controller, Service, Repository, API, DTO, Entity, Database, DB, SQL, SaveChanges, Publish, Endpoint, Hub, RabbitMQ, MassTransit, Map/Mapper, Exception, Guid, Id, [Authorize], Identity, User, Filter, Include, Join...
   - PHẢI DÙNG: Người bán, người mua, bài đấu giá, tài sản, mức giá, gian lận, lỗi hệ thống, mất dữ liệu, quyền hạn, thông báo cho phân hệ khác, đồng bộ thông tin...
2. ĐẶT CÂU HỎI THEO DẠNG TÌNH HUỐNG THỰC TẾ (Scenario-based):
   - Thay vì hỏi về code xử lý lỗi, hãy hỏi: ""Nếu hệ thống đang lưu thông tin bài đấu giá mới mà bị sập nguồn hoặc mất kết nối giữa chừng, điều gì xảy ra? Khách hàng có bị ảnh hưởng không?""
   - Thay vì hỏi về quyền trong code, hãy hỏi: ""Cơ chế nào ngăn một người dùng bình thường tự ý vào sửa đổi hoặc xóa bài đấu giá của người khác trên sàn?""
3. CẤU TRÚC CÂU TRẢ LỜI GỢI Ý (SUGGESTED ANSWER):
   - Phải giải thích hoàn toàn bằng ngôn ngữ nghiệp vụ thực tế (Hệ thống xử lý logic gì, chặn ở bước nào, dữ liệu được đồng bộ đi đâu). 
   - Tuyệt đối không đưa tên hàm hay tên file vào câu trả lời. Giảng viên chấm thi chỉ cần nghe sinh viên giải thích được bản chất logic nghiệp vụ là đủ.
4. QUY TẮC TRUY VẾT LUỒNG CODE (TARGETED ENTRY POINTS):
   - Mảng ""targetedEntryPoints"" PHẢI mô tả chính xác luồng đi của dữ liệu (Call Stack) từ tầng cao xuống tầng thấp để xử lý tình huống được hỏi.
   - QUY ĐỊNH BẮT BUỘC: Mỗi mảng PHẢI chứa ít nhất 3 phần tử theo đúng thứ tự luồng: 
     [ ""TênController.TênAction"", ""TênInterfaceService.TênHàmAsync"", ""TênClassServiceImpl.TênHàmAsync"" ]
   - Ví dụ chuẩn luồng Tạo bài đấu giá: 
     [""AuctionGeneratorController.GenerateUnifiedQuestions"", ""IAuctionService.CreateAuctionAsync"", ""AuctionServiceImpl.CreateAuctionAsync""]
   - Tuyệt đối KHÔNG ĐƯỢC bỏ sót bất kỳ mắt xích nào trong 3 tầng này. Nếu tình huống chạm tới tầng lưu trữ, có thể thêm phần tử thứ 4 là Repository (Interface và Class).
ĐỊNH DẠNG ĐẦU RA BẮT BUỘC:
- Trả về một mảng JSON chứa các đối tượng có cấu trúc chính xác như sau (Tuyệt đối không bọc mảng trong ký tự ```json, chỉ trả về JSON trần):
[
  {{
    ""question"": ""Câu hỏi nghiệp vụ ở đây"",
    ""suggestedAnswer"": ""Câu trả lời bám sát logic nghiệp vụ ở đây"",
    ""difficulty"": ""{difficulty}"",
    ""targetedEntryPoints"": [""Ten ham"", ""Ten ham""]
  }}
]";

            var prompt = new StringBuilder();
           

            prompt.AppendLine("--- THÔNG TIN CHUNG ---");
            prompt.AppendLine($"Tên Business: {businessName}");
            prompt.AppendLine();
            
            prompt.AppendLine("--- THÔNG TIN DATA FLOW GRAPH ---");
            prompt.AppendLine(contextBuilder);
            prompt.AppendLine();

            prompt.AppendLine("--- SOURCE CODE CHI TIẾT (CODEBASE) ---");
            prompt.AppendLine(codeBuilder);
            prompt.AppendLine();

            if (!string.IsNullOrWhiteSpace(additionalContext))
            {
                prompt.AppendLine("--- HƯỚNG DẪN BỔ SUNG TỪ GIẢNG VIÊN ---");
                prompt.AppendLine(additionalContext);
                prompt.AppendLine();
            }

            // Nhúng few-shot examples vào prompt
            var examplesList = fewShotExamples?.ToList();
            if (examplesList != null && examplesList.Count > 0)
            {
                prompt.AppendLine("--- VÍ DỤ CÂU HỎI CHUẨN CỦA GIẢNG VIÊN (FEW-SHOT EXAMPLES) ---");
                prompt.AppendLine("Dưới đây là các câu hỏi mẫu do giảng viên cung cấp. Hãy học theo phong cách, độ sâu nghiệp vụ và cấu trúc câu trả lời của chúng:");
                prompt.AppendLine();
                for (int i = 0; i < examplesList.Count; i++)
                {
                    var ex = examplesList[i];
                    prompt.AppendLine($"[Ví dụ {i + 1}]");
                    prompt.AppendLine($"  Câu hỏi   : {ex.Question}");
                    prompt.AppendLine($"  Đáp án    : {ex.SuggestedAnswer}");
                    prompt.AppendLine($"  Mức độ    : {ex.Difficulty}");
                    if (!string.IsNullOrWhiteSpace(ex.Tag))
                        prompt.AppendLine($"  Nhãn      : {ex.Tag}");
                    prompt.AppendLine();
                }
            }
            prompt.AppendLine($"Hãy trả về chính xác ĐÚNG {numberOfQuestions} câu hỏi mức độ {difficulty} theo cấu trúc object gồm các key: question, suggestedAnswer, difficulty.");


            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = [new Part { Text = systemInstruction }]
                },
                Temperature = 0.3f, 
                ResponseMimeType = "application/json"
            };

            int maxRetries = 5;
            int delaySeconds = 5;
            GenerateContentResponse? response = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    response = await _client.Models.GenerateContentAsync(
                        model: "gemini-3.1-flash-lite",
                        contents: prompt.ToString(),
                        config: config
                    );
                    break;
                }
                catch (ClientError ex) when (ex.StatusCode == 429 || ex.StatusCode == 503 || ex.StatusCode == 500)
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AIService] Transient error {ex.StatusCode} (Unified). Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AIService] Rate limit (Unified). Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
            }

            if (response == null || string.IsNullOrEmpty(response.Text))
                throw new Exception("AI API did not return any text response for unified generation.");

            string aiJsonText = response.Text.Trim();

            try
            {
                var questions = JsonSerializer.Deserialize<List<GeneratedQuestionDto>>(aiJsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<GeneratedQuestionDto>();
                 return questions;

            }
            catch (JsonException ex)
            {
                throw new Exception($"AI trả về cấu trúc JSON không khớp với DTO. Nội dung: {aiJsonText}. Chi tiết lỗi: {ex.Message}", ex);
            }
        }

       
    }
}




