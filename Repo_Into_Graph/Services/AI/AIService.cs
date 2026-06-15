using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.AI
{
    public class AIService : IAIService
    {
        private readonly GenerativeModel _model;

        public AIService(IConfiguration configuration)
        {
            var apiKey = configuration["GeminiConfig:ApiKey"];
            var googleAI = new GoogleAI(apiKey: apiKey);
            _model = googleAI.GenerativeModel(model: Model.Gemini20Flash);
        }

        public async Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestions(int numberOfQuestions,
            GenerateQuestionsRequest request,
            string codeBuilder, string contextBuilder)
        {
            // Build the AI prompt
            var prompt = new StringBuilder();
            prompt.AppendLine("You are an AI assistant designed to help university lecturers conduct viva/oral exams for student programming projects.");
            prompt.AppendLine("Analyze the following source code and its call graph context to generate a list of questions.");
            prompt.AppendLine("These questions should test the student's deep understanding of the code logic, architecture, design choices, data flow, and error handling.");
            prompt.AppendLine();
            prompt.AppendLine($"Target number of questions: {numberOfQuestions}");
            prompt.AppendLine($"Difficulty level: {request.Difficulty}");
            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                prompt.AppendLine($"Additional instructions/focus area: {request.Description}");
            }
            prompt.AppendLine();
            prompt.AppendLine("--- SOURCE CODE ---");
            //prompt.AppendLine(codeBuilder);
            prompt.AppendLine("public int Add(int a, int b) { return a + b; }");
            prompt.AppendLine();
            prompt.AppendLine("--- CALL GRAPH CONTEXT ---");
            prompt.AppendLine(contextBuilder);
            prompt.AppendLine();
            prompt.AppendLine("IMPORTANT: Respond ONLY with a valid JSON array of objects. Do not wrap the JSON in ```json or any other text. Each object must have these exact keys:");
            prompt.AppendLine("- \"question\": (string) The question to ask the student.");
            prompt.AppendLine("- \"suggestedAnswer\": (string) A concise, clear guide answer for the lecturer to grade the student.");
            prompt.AppendLine("- \"difficulty\": (string) The difficulty of this question (Easy, Medium, Hard).");

            var response = await _model.GenerateContent(prompt.ToString());

            if (response == null || string.IsNullOrEmpty(response.Text))
                throw new Exception("AI API did not return any text response.");

            string responseString = response.Text;
            string aiJsonText = response.Text.Trim();

            if (aiJsonText.StartsWith("```json"))
            {
                aiJsonText = aiJsonText.Substring(7, aiJsonText.Length - 10).Trim();
            }
            else if (aiJsonText.StartsWith("```"))
            {
                aiJsonText = aiJsonText.Substring(3, aiJsonText.Length - 6).Trim();
            }

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
                throw new Exception($"AI trả về cấu trúc JSON không hợp lệ. Nội dung AI trả về: {aiJsonText}. Chi tiết lỗi: {ex.Message}");
            }
        }
    }
}



