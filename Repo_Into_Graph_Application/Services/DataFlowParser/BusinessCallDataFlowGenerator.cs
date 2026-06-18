using Repo_Into_Graph_DataAccess.Models.BusinessFlows;
using Repo_Into_Graph_DataAccess.Models.BusinessFlows;

using Repo_Into_Graph_DataAccess.Models.Method;
using System.Collections.Generic;
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

        public string GenerateCallDataFlow(BusinessFlow businessFlow, List<MethodSourceRecord> allMethodSources, List<DataFlowEdge> allIntraEdges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("graph TD"); 

            var renderedLines = new HashSet<string>();
            var declaredNodes = new HashSet<string>();

      
            var orderedSteps = businessFlow.Steps.OrderBy(s => s.StepOrder).ToList();
            int orderCounter = 1;

            foreach (var step in orderedSteps)
            {
                string callerNodeId = Clean($"{step.CallerClass}_{step.CallerMethod}");
                string calleeNodeId = Clean($"{step.CalleeClass}_{step.CalleeMethod}");

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
                string callerNodeId = Clean($"{step.CallerClass}_{step.CallerMethod}");
                string calleeNodeId = Clean($"{step.CalleeClass}_{step.CalleeMethod}");

               
                string targetCalleeClass = step.CalleeClass;

               
                var nextBindingStep = businessFlow.Steps
                    .FirstOrDefault(s => s.StepOrder > step.StepOrder && s.CallerClass == step.CalleeClass && s.CallerMethod == step.CalleeMethod);

                if (nextBindingStep != null)
                {
                    targetCalleeClass = nextBindingStep.CalleeClass;
                }

                var calleeSource = allMethodSources
                    .FirstOrDefault(s => s.ClassName == targetCalleeClass && s.MethodName == step.CalleeMethod);

                string inputLabel = "G?i hàm";
                if (calleeSource != null)
                {
                    var calleeParams = _parserService.GetMethodParameters(calleeSource.SourceCode);
                    if (calleeParams != null && calleeParams.Any())
                    {
                        var cleanParams = calleeParams.Select(p => p.Replace("(", "").Replace(")", "").Replace("\"", "").Trim());
                        inputLabel = $"Truy?n: {string.Join(", ", cleanParams)}";
                    }
                }
                else
                {
                    if (step.CalleeMethod.Contains("Add")) inputLabel = "Ð?y th?c th?";
                    else if (step.CalleeMethod.Contains("Save")) inputLabel = "L?nh luu d? li?u";
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
                        outputLabel = "Tr? v?: true/false";
                    }
                    else
                    {
                        string cleanToken = returnToken.Replace("(", "").Replace(")", "").Replace("\"", "").Replace(";", "").Trim();
                        if (cleanToken.Length > 30) cleanToken = "k?t qu?";
                        outputLabel = $"Tr? ra: {cleanToken}";
                    }

                    string returnLine = $"    {calleeNodeId} -.->| {orderCounter++}. {outputLabel} | {callerNodeId}";
                    if (!renderedLines.Contains(returnLine))
                    {
                        sb.AppendLine(returnLine);
                        renderedLines.Add(returnLine);
                    }
                }
            }

            return sb.ToString();
        }

        private string Clean(string text)
        {
            return text
                .Replace(".", "_")
                .Replace("(", "")
                .Replace(")", "")
                .Replace(" ", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("[", "_")
                .Replace("]", "_")
                .Replace("\"", "");
        }
    }
}




