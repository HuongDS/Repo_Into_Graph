
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Google.GenAI;
using Google.GenAI.Types;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using FeatureModel = Repo_Into_Graph_DataAccess.Models.Feature.Feature;
using Repo_Into_Graph_DataAccess.Models.FewShot;
using Repo_Into_Graph_Application.Dtos.QuestionEvalution;
using Type = Google.GenAI.Types.Type;




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

        public async Task<IEnumerable<QuestionEvaluationResultDto>> EvaluateQuestionsAsync(string dataFlowMermaidGraph, string codeBuilder, IEnumerable<GeneratedQuestionDto> generatedQuestions)
        {
            if (generatedQuestions == null || !generatedQuestions.Any())
                return new List<QuestionEvaluationResultDto>();

            var systemInstruction = """
    Bạn là một Chuyên gia Kiểm định Chất lượng Phần mềm (Senior QA Engineer) và là Hội đồng Thẩm định AI phục vụ cho Đề tài Nghiên cứu Khoa học.
    Nhiệm vụ của bạn là phân tích mã nguồn (Source Code) và biểu đồ luồng dữ liệu (Data Flow Mermaid) được cung cấp để làm CHÂN LÝ (Ground Truth).
    Sau đó, hãy đánh giá chất lượng các cặp Câu hỏi (question) và Câu trả lời gợi ý (suggestedAnswer) được sinh ra tự động từ hệ thống xem có khớp với Chân lý hay không.
                
    Hãy duyệt qua từng câu hỏi trong danh sách và chấm điểm từ 1 đến 10 dựa trên 3 tiêu chí cốt lõi:
    1. factualCorrectness: Độ chính xác thực tế của câu trả lời so với Logic xử lý trong Source Code và luồng đi của Data Flow Mermaid.
    2. relevanceCompleteness: Điểm độ liên quan, giải quyết trọn vẹn câu hỏi, không viết dài dòng lan man.
    3. technicalClarity: Việc sử dụng chính xác các thuật ngữ công nghệ chuyên ngành (ví dụ: batch insert, validation rules, commit...).
    """;

            string questionsJsonContext = JsonSerializer.Serialize(generatedQuestions, new JsonSerializerOptions { WriteIndented = true });

            var prompt = new StringBuilder();
            prompt.AppendLine("--- SOURCE CODE CHI TIẾT (GROUND TRUTH) ---");
            prompt.AppendLine(codeBuilder);
            prompt.AppendLine();

            prompt.AppendLine("--- BIỂU ĐỒ LUỒNG ĐỐI CHIẾU (DATA FLOW MERMAID) ---");
            prompt.AppendLine(dataFlowMermaidGraph);
            prompt.AppendLine();

            prompt.AppendLine("--- DANH SÁCH CẶP CÂU HỎI VÀ CÂU TRẢ LỜI CẦN CHẤM ĐIỂM ---");
            prompt.AppendLine(questionsJsonContext);
            prompt.AppendLine();

            // Chuẩn hóa lại Prompt theo CamelCase đồng bộ với DTO C#
            prompt.AppendLine("Hãy đánh giá toàn bộ các câu hỏi trên. Xuất ra kết quả BẮT BUỘC là một JSON Array chứa các thuộc tính: question, suggestedAnswer, scores (gồm factualCorrectness, relevanceCompleteness, technicalClarity, averageTotalScore), và evaluationDetails (gồm factualCorrectnessReason, relevanceCompletenessReason, technicalClarityReason). KHÔNG bọc ngoài bằng bất kỳ key nào khác.");

            // Cấu hình Config nâng cấp cho Gemini Pro kèm ResponseSchema
            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = [new Part { Text = systemInstruction }]
                },
                Temperature = 0.2f, // Hạ xuống 0.2 để kết quả chấm điểm mang tính nhất quán và kỷ luật hơn
                ResponseMimeType = "application/json",
                ResponseSchema = new Schema
                {
                    Type = Type.Array,
                    Items = new Schema
                    {
                        Type = Type.Object,
                        Properties = new Dictionary<string, Schema>
                {
                    { "question", new Schema { Type = Type.String } },
                    { "suggestedAnswer", new Schema { Type = Type.String, Nullable = true } },
                    {
                        "scores", new Schema
                        {
                            Type = Type.Object,
                            Properties = new Dictionary<string, Schema>
                            {
                                { "factualCorrectness", new Schema { Type = Type.Integer } },
                                { "relevanceCompleteness", new Schema { Type = Type.Integer } },
                                { "technicalClarity", new Schema { Type = Type.Integer } },
                                { "averageTotalScore", new Schema { Type = Type.Number } }
                            },
                            Required = ["factualCorrectness", "relevanceCompleteness", "technicalClarity", "averageTotalScore"]
                        }
                    },
                    {
                        "evaluationDetails", new Schema
                        {
                            Type = Type.Object,
                            Nullable = true,
                            Properties = new Dictionary<string, Schema>
                            {
                                { "factualCorrectnessReason", new Schema { Type = Type.String } },
                                { "relevanceCompletenessReason", new Schema { Type = Type.String } },
                                { "technicalClarityReason", new Schema { Type = Type.String } }
                            },
                            Required = ["factualCorrectnessReason", "relevanceCompletenessReason", "technicalClarityReason"]
                        }
                    }
                },
                        Required = ["question", "scores"]
                    }
                }
            };

            int maxRetries = 3;
            int delaySeconds = 3;
            GenerateContentResponse? response = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    response = await _client.Models.GenerateContentAsync(
                        model: "gemini-3.1-pro",
                        contents: prompt.ToString(),
                        config: config
                    );
                    break;
                }
                catch (ClientError ex) when (ex.StatusCode == 429 || ex.StatusCode == 503 || ex.StatusCode == 500)
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AiJudge] Transient error {ex.StatusCode}. Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AiJudge] Rate limit error. Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
            }

            if (response == null || string.IsNullOrEmpty(response.Text))
                throw new Exception("AI Judge API did not return any evaluation text response.");

            string aiJsonText = response.Text.Trim();

            try
            {
                var evaluationResults = JsonSerializer.Deserialize<List<QuestionEvaluationResultDto>>(aiJsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return evaluationResults ?? new List<QuestionEvaluationResultDto>();
            }
            catch (JsonException ex)
            {
                throw new Exception($"AI Judge trả về cấu trúc JSON không khớp với DTO đánh giá. Nội dung: {aiJsonText}. Chi tiết lỗi: {ex.Message}", ex);
            }
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
            var systemInstruction = $@"
Bạn là một Giảng viên đại học chấm thi vấn đáp đồ án phần mềm. Nhiệm vụ của bạn là dựa vào Mã nguồn (Source Code) và Sơ đồ luồng (Mermaid Graph) để tìm ra các Quy tắc nghiệp vụ (Business Rules), sau đó đặt câu hỏi kiểm tra xem sinh viên có hiểu ""Hệ thống này vận hành trên thực tế như thế nào"" hay không.

YÊU CẦU BẮT BUỘC VỀ SỐ LƯỢNG VÀ ĐỘ KHÓ:
- Bạn PHẢI tạo ra chính xác ĐÚNG {numberOfQuestions} câu hỏi. Không được tạo nhiều hơn hoặc ít hơn.
- Tất cả các câu hỏi phải được thiết kế ở mức độ: {difficulty}. 
  (Với mức độ ""Khó"", câu hỏi phải xoay quanh các lỗ hổng logic, kịch bản lỗi, hoặc bài toán đồng bộ dữ liệu giữa các phân hệ).

QUY TẮC ĐẶT CÂU HỎI VÀ TRẢ LỜI (THIẾT QUÂN LUẬT - NGHIÊM CẤM TỪ KHÓA KỸ THUẬT):
1. NGÔN NGỮ THUỒN NGHIỆP VỤ (100% Business Language): Cả câu hỏi (question) và câu trả lời (suggestedAnswer) KHÔNG ĐƯỢC CHỨA bất kỳ từ khóa kỹ thuật, tên framework, hay cấu trúc code nào.
   - CẤM DÙNG: Controller, Service, Repository, API, DTO, Entity, Database, DB, SQL, SaveChanges, Publish, Endpoint, Hub, RabbitMQ, MassTransit, Map/Mapper, Exception, Guid, Id, [Authorize], Identity, User, Filter, Include, Join...
   - PHẢI DÙNG: Người bán, người mua, bài đấu giá, tài sản, mức giá, gian lận, lỗi hệ thống, mất dữ liệu, quyền hạn, thông báo cho phân hệ khác, đồng bộ thông tin...

2. ĐẶT CÂU HỎI THEO DẠNG TÌNH HUỐNG THỰC TẾ (Scenario-based):
   - Thay vì hỏi về code xử lý lỗi, hãy hỏi: ""Nếu hệ thống đang lưu thông tin bài đấu giá mới mà bị sập nguồn hoặc mất kết nối giữa chừng, điều gì xảy ra? Khách hàng có bị ảnh hưởng không?""
   - Thay vì hỏi về quyền trong code, hãy hỏi: ""Cơ chế nào ngăn một người dùng bình thường tự ý vào sửa đổi hoặc xóa bài đấu giá của người khác trên sàn?""
   - Thay vì hỏi về hàm lọc, hãy hỏi: ""Khi người mua tìm kiếm tài sản đấu giá, hệ thống đang ưu tiên hiển thị và lọc các sản phẩm theo những quy tắc cụ thể nào?""

3. CẤU TRÚC CÂU TRẢ LỜI GỢI Ý (SUGGESTED ANSWER):
   - Phải giải thích hoàn toàn bằng ngôn ngữ nghiệp vụ thực tế (Hệ thống xử lý logic gì, chặn ở bước nào, dữ liệu được đồng bộ đi đâu). 
   - Tuyệt đối không đưa tên hàm hay tên file vào câu trả lời. Giảng viên chấm thi chỉ cần nghe sinh viên giải thích được bản chất logic nghiệp vụ là đủ.

ĐỊNH DẠNG ĐẦU RA BẮT BUỘC:
- Trả về một mảng JSON chứa các đối tượng có cấu trúc chính xác như sau:
{{
  ""question"": ""Câu hỏi nghiệp vụ ở đây"",
  ""suggestedAnswer"": ""Câu trả lời bám sát logic nghiệp vụ ở đây"",
  ""difficulty"": ""{difficulty}""
}}
";

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
                Temperature = 0.3f, // Cân bằng giữa code và tính sáng tạo của Business
                ResponseMimeType = "application/json"
            };

            int maxRetries = 3;
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
                });
                return questions ?? new List<GeneratedQuestionDto>();
            }
            catch (JsonException ex)
            {
                throw new Exception($"AI trả về cấu trúc JSON không khớp với DTO. Nội dung: {aiJsonText}. Chi tiết lỗi: {ex.Message}", ex);
            }
        }
    }
}




