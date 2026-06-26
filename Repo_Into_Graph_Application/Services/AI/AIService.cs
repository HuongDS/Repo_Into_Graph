
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
            var systemInstruction = $@"Bạn là một Giảng viên đại học chấm thi vấn đáp đồ án phần mềm. Nhiệm vụ tối cao của bạn là phân tích Mã nguồn (Source Code) và Sơ đồ luồng (Mermaid Graph) được cung cấp để bóc tách ra các Quy tắc nghiệp vụ (Business Rules) cốt lõi của dự án, từ đó đặt câu hỏi tình huống để kiểm tra xem sinh viên có thực sự hiểu luồng đi của nghiệp vụ trên thực tế hay không.

YÊU CẦU BẮT BUỘC VỀ SỐ LƯỢNG VÀ ĐỘ KHÓ:
- Bạn PHẢI tạo ra chính xác ĐÚNG {numberOfQuestions} câu hỏi. Không được tạo nhiều hơn hoặc ít hơn.
- Tất cả các câu hỏi phải được thiết kế ở mức độ: {difficulty}. (Với mức độ ""Khó"", câu hỏi phải xoáy sâu vào: Lỗ hổng logic nghiệp vụ, kịch bản lỗi khi vận hành, bài toán đồng bộ/xung đột dữ liệu giữa các phân hệ, hoặc rủi ro gian lận nghiệp vụ).

THIẾT QUÂN LUẬT VỀ NGÔN NGỮ (100% BUSINESS LANGUAGE):
1. Cả câu hỏi (question) và câu trả lời (suggestedAnswer) TUYỆT ĐỐI KHÔNG CHỨA bất kỳ từ khóa kỹ thuật hay cấu trúc mã nguồn nào.
   - CẤM DÙNG: Controller, Service, Repository, API, DTO, Entity, Database, DB, SQL, SaveChanges, Update, Delete, Publish, Endpoint, Hub, RabbitMQ, MassTransit, Map/Mapper, Exception, Guid, Id, [Authorize], Identity, User, Filter, Include, Join...
   - PHẢI DÙNG: Người bán, người mua, bài đấu giá, tài sản, mức giá, gian lận, lỗi hệ thống, mất dữ liệu, quyền hạn, thông báo cho phân hệ khác, đồng bộ thông tin...
2. Đặt câu hỏi theo dạng tình huống thực tế (Scenario-based): Khảo sát trực tiếp vào quy trình vận hành (Ví dụ: ""Nếu người bán cố tình chỉnh sửa thông tin khi đã có người đặt giá thành công..."", ""Nếu hệ thống ghi nhận thanh toán nhưng việc cập nhật trạng thái bài đấu giá bị gián đoạn..."").

QUY TẮC BẮT BUỘC VỀ TRUY VẾT LUỒNG CODE (TARGETED ENTRY POINTS):
- Mảng ""targetedEntryPoints"" PHẢI mô tả chính xác và đầy đủ Call Stack (luồng đi từ Controller xuống tầng xử lý và lưu trữ) để giải quyết tình huống nghiệp vụ được hỏi.
- QUY ĐỊNH CẤU TRÚC MẢNG: Mỗi mảng PHẢI chứa đầy đủ các mắt xích theo thứ tự từ trên xuống dưới (Ít nhất 3 đến 4 phần tử tùy luồng):
  [ ""TênController.TênAction"", ""TênInterfaceService.TênHàmAsync"", ""TênClassServiceImpl.TênHàmAsync"", ""TênRepository.TênHàmAsync (nếu có)"" ]
- VÍ DỤ MẪU CHUẨN LUỒNG:
  [ ""AuctionController.CreateAuction"", ""IAuctionService.CreateAuctionAsync"", ""AuctionServiceImpl.CreateAuctionAsync"", ""IAuctionRepository.AddAsync"" ]
- TUYỆT ĐỐI KHÔNG ĐƯỢC bỏ sót việc bắt cặp giữa [Interface] và [Class triển khai]. Nếu thiếu bất kỳ tầng nào, kết quả sẽ bị coi là bất hợp lệ.

ĐỊNH DẠNG ĐẦU RA BẮT BUỘC:
- Trả về một mảng JSON chứa các đối tượng có cấu trúc chính xác như sau (Tuyệt đối không bọc mảng trong ký tự ```json, chỉ trả về JSON trần):
[
  {{
    ""question"": ""Câu hỏi tình huống nghiệp vụ thực tế ở đây"",
    ""suggestedAnswer"": ""Giải thích giải pháp xử lý logic nghiệp vụ ở đây (không chứa từ khóa code)"",
    ""difficulty"": ""{difficulty}"",
    ""targetedEntryPoints"": [
      ""TênController.TênAction"",
      ""TênInterfaceService.TênHàmAsync"",
      ""TênClassServiceImpl.TênHàmAsync""
    ]
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
            int delaySeconds = 3;
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




