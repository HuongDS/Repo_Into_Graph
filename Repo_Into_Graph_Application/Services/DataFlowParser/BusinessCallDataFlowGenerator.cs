using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Models.Method;
using System.Linq;
using System.Text;

namespace Repo_Into_Graph_Application.Services.DataFlowParser
{
    public class BusinessCallDataFlowGenerator
    {
        private readonly DataFlowParseService _parserService;

        public BusinessCallDataFlowGenerator(DataFlowParseService parserService)
        {
            _parserService = parserService;
        }

        public string GenerateCallDataFlow(Feature feature, List<MethodSourceRecord> allMethodSources, List<DataFlowEdge> allIntraEdges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("graph TD");

            var renderedLines = new HashSet<string>();
            var declaredNodes = new HashSet<string>();

            var orderedSteps = feature.Steps.OrderBy(s => s.StepOrder).ToList();
            int orderCounter = 1;

            foreach (var step in orderedSteps)
            {
                string callerNodeId = CleanNodeId($"{step.CallerClass}_{step.CallerMethod}");
                string calleeNodeId = CleanNodeId($"{step.CalleeClass}_{step.CalleeMethod}");

                if (!declaredNodes.Contains(callerNodeId))
                {
                    sb.AppendLine($"    {callerNodeId}[\"{step.CallerClass}.{step.CallerMethod}\"]");
                    declaredNodes.Add(callerNodeId);
                }
                if (!declaredNodes.Contains(calleeNodeId))
                {
                    sb.AppendLine($"    {calleeNodeId}[\"{step.CalleeClass}.{step.CalleeMethod}\"]");
                    declaredNodes.Add(calleeNodeId);
                }
            }

            foreach (var step in orderedSteps)
            {
                string callerNodeId = CleanNodeId($"{step.CallerClass}_{step.CallerMethod}");
                string calleeNodeId = CleanNodeId($"{step.CalleeClass}_{step.CalleeMethod}");

                string targetCalleeClass = step.CalleeClass;

                var nextBindingStep = feature.Steps
                    .FirstOrDefault(s => s.StepOrder > step.StepOrder && s.CallerClass == step.CalleeClass && s.CallerMethod == step.CalleeMethod);

                if (nextBindingStep != null)
                {
                    targetCalleeClass = nextBindingStep.CalleeClass;
                }

                var calleeSource = allMethodSources
                    .FirstOrDefault(s => s.ClassName == targetCalleeClass && s.MethodName == step.CalleeMethod);

                string inputLabel = "Gọi hàm";
                if (calleeSource != null)
                {
                    var calleeParams = _parserService.GetMethodParameters(calleeSource.SourceCode);
                    if (calleeParams != null && calleeParams.Any())
                    {
                        var cleanParams = calleeParams.Select(p => CleanLabel(p));
                        inputLabel = $"Truyền: {string.Join(", ", cleanParams)}";
                    }
                }
                else
                {
                    if (step.CalleeMethod.Contains("Add")) inputLabel = "Đẩy thực thể";
                    else if (step.CalleeMethod.Contains("Save")) inputLabel = "Lệnh lưu dữ liệu";
                }

                string callLine = $"    {callerNodeId} -->| {orderCounter++}. {inputLabel} | {calleeNodeId}";
                if (!renderedLines.Contains(callLine))
                {
                    sb.AppendLine(callLine);
                    renderedLines.Add(callLine);
                }

                var returnEdge = allIntraEdges.FirstOrDefault(e =>
                    e.ClassName == targetCalleeClass &&
                    e.MethodName == step.CalleeMethod &&
                    e.RelationType == "Return");

                if (returnEdge != null && returnEdge.SourceToken != "RETURN_VALUE")
                {
                    string returnToken = returnEdge.SourceToken;
                    string outputLabel = "";

                    if (returnToken.Contains(">") || returnToken.Contains("<") || returnToken.Contains("=="))
                    {
                        outputLabel = "Trở về: true_false";
                    }
                    else
                    {
                        string cleanToken = CleanLabel(returnToken);
                        if (cleanToken.Length > 30) cleanToken = "kết quả";
                        outputLabel = $"Trả ra: {cleanToken}";
                    }

                    string returnLine = $"    {calleeNodeId} -.->| {orderCounter++}. {outputLabel} | {callerNodeId}";
                    if (!renderedLines.Contains(returnLine))
                    {
                        sb.AppendLine(returnLine);
                        renderedLines.Add(returnLine);
                    }
                }
            }

            string rawGraph = sb.ToString();
            string cleanGraph = rawGraph
               .Replace("\u00A0", " ")
               .Replace("\r\n", "\n")
               .Replace("\r", "\n");

            return cleanGraph.Trim();
        }

        private string CleanNodeId(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return text
                .Replace("\u00A0", " ")
                .Replace(".", "_")
                .Replace("(", "")
                .Replace(")", "")
                .Replace(" ", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("[", "_")
                .Replace("]", "_")
                .Replace("?", "")
                .Replace("\"", "")
                .Trim();
        }

        private string CleanLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return text
                .Replace("\u00A0", " ")
                .Replace("(", "[") 
                .Replace(")", "]")
                .Replace("<", "&lt;") 
                .Replace(">", "&gt;")
                .Replace("?", "")   
                .Replace("\"", "")
                .Replace(";", "")
                .Trim();
        }
    }
}