using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Repo_Into_Graph_Application.Dtos.LLMOrchestrator
{
    public class GeminiQuestionResponse
    {
        [JsonPropertyName("questions")]
        public List<CodeQuestion> Questions { get; set; } = new List<CodeQuestion>();

        [JsonPropertyName("metaData")]
        public ResponseMetaData? MetaData { get; set; }
    }

    public class ResponseMetaData
    {
        [JsonPropertyName("totalGenerated")]
        public int TotalGenerated { get; set; }

        [JsonPropertyName("topic")]
        public string Topic { get; set; } = string.Empty;
    }
}
