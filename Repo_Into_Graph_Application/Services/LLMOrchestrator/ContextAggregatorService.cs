using System.Collections.Generic;
using System.Text;
using Repo_Into_Graph_Application.Dtos.LLMOrchestrator;

namespace Repo_Into_Graph_Application.Services.LLMOrchestrator
{
    public class ContextAggregatorService : IContextAggregatorService
    {
        // 1 Token ~ 4 Characters (Heuristic)
        private const int MAX_TOKENS = 32000;
        private const int MAX_CHARS_LIMIT = MAX_TOKENS * 4;

        public string AggregateContext(IEnumerable<FunctionNode> nodes)
        {
            var builder = new StringBuilder();
            bool hasWarned = false;

            foreach (var node in nodes)
            {
                builder.AppendLine($"### Hàm/Phương thức: {node.NodeName}");

                if (node.RouteType == "ROUTE_RAW_CODE")
                {
                    builder.AppendLine($"```{node.Language}");
                    builder.AppendLine(node.SourceCode);
                    builder.AppendLine("```");
                }
                else if (node.RouteType == "ROUTE_HYBRID")
                {
                    builder.AppendLine("#### CFG Skeleton:");
                    builder.AppendLine("```mermaid");
                    builder.AppendLine(node.CfgSkeleton);
                    builder.AppendLine("```");

                    builder.AppendLine("#### Critical Snippets:");
                    builder.AppendLine($"```{node.Language}");
                    builder.AppendLine(node.CriticalSnippets);
                    builder.AppendLine("```");
                }
                
                builder.AppendLine();

                // Cảnh báo (Soft-Warning) nếu vượt ngưỡng Token Window, nhưng không chặn luồng chạy
                if (!hasWarned && builder.Length > MAX_CHARS_LIMIT)
                {
                    int estimatedTokens = builder.Length / 4;
                    Console.WriteLine($"⚠️ [WARNING] Ngữ cảnh rất lớn ({estimatedTokens}+ tokens, vượt mức {MAX_TOKENS} tokens). Có thể làm giảm độ chính xác hoặc vượt quá Context Window của Gemini.");
                    hasWarned = true;
                }
            }

            return builder.ToString();
        }
    }
}
