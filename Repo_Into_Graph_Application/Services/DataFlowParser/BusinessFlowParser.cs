using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.BusinessFlows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Repo_Into_Graph_Application.Services.DataFlowParser
{
    public class BusinessFlowParser
    {
        public List<BusinessFlow> ParseBusinessFlows(Guid analysisRunId, List<CallGraphEdge> edges)
        {
            var businessFlows = new List<BusinessFlow>();

            if (edges == null || !edges.Any())
                return businessFlows;

            // 1. Group edges by Caller (Class::Method)
            var graphLookup = edges.ToLookup(
                e => $"{e.CallerClass.Trim().ToLower()}::{e.CallerMethod.Trim().ToLower()}"
            );

            // 2. Identify Controllers (Entry Points)
            // Any CallerClass ending with "Controller"
            var controllerMethods = edges
                .Where(e => e.CallerClass.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                .Select(e => new { Class = e.CallerClass, Method = e.CallerMethod })
                .Distinct()
                .ToList();

            foreach (var start in controllerMethods)
            {
                var flowId = Guid.NewGuid();
                var flowName = $"{start.Class}.{start.Method}";
                var entryPoint = $"{start.Class}.{start.Method}";

                var steps = new List<BusinessFlowStep>();
                var visited = new HashSet<string>();
                int order = 1;

                // Trace paths using Queue
                var queue = new Queue<(string NodeKey, string ClassName, string MethodName)>();
                queue.Enqueue(($"{start.Class.ToLower()}::{start.Method.ToLower()}", start.Class, start.Method));
                visited.Add($"{start.Class.ToLower()}::{start.Method.ToLower()}");

                var flowEdges = new List<(string From, string To)>();

                while (queue.Count > 0)
                {
                    var (nodeKey, currentClass, currentMethod) = queue.Dequeue();

                    if (graphLookup.Contains(nodeKey))
                    {
                        foreach (var edge in graphLookup[nodeKey])
                        {
                            var calleeKey = $"{edge.CalleeClass.Trim().ToLower()}::{edge.CalleeMethod.Trim().ToLower()}";

                            steps.Add(new BusinessFlowStep
                            {
                                Id = Guid.NewGuid(),
                                BusinessFlowId = flowId,
                                CallerClass = edge.CallerClass,
                                CallerMethod = edge.CallerMethod,
                                CalleeClass = edge.CalleeClass,
                                CalleeMethod = edge.CalleeMethod,
                                StepOrder = order++,
                                CreatedAt = DateTime.UtcNow
                            });

                            flowEdges.Add(($"{edge.CallerClass}.{edge.CallerMethod}", $"{edge.CalleeClass}.{edge.CalleeMethod}"));

                            if (!visited.Contains(calleeKey))
                            {
                                visited.Add(calleeKey);
                                queue.Enqueue((calleeKey, edge.CalleeClass, edge.CalleeMethod));
                            }
                        }
                    }
                }

                if (steps.Any())
                {
                    var mermaid = GenerateMermaidForFlow(flowEdges);
                    businessFlows.Add(new BusinessFlow
                    {
                        Id = flowId,
                        AnalysisRunId = analysisRunId,
                        Name = flowName,
                        EntryPoint = entryPoint,
                        MermaidGraph = mermaid,
                        CreatedAt = DateTime.UtcNow,
                        Steps = steps
                    });
                }
            }

            return businessFlows;
        }

        private string GenerateMermaidForFlow(List<(string From, string To)> edges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("graph TD");

            var nodeIds = new Dictionary<string, string>();
            int nodeCounter = 0;

            string GetNodeId(string nodeName)
            {
                if (!nodeIds.TryGetValue(nodeName, out var id))
                {
                    id = $"N{nodeCounter++}";
                    nodeIds[nodeName] = id;
                    sb.AppendLine($"    {id}[\"{SanitizeMermaid(nodeName)}\"]");
                }
                return id;
            }

            foreach (var edge in edges)
            {
                var callerId = GetNodeId(edge.From);
                var calleeId = GetNodeId(edge.To);
                sb.AppendLine($"    {callerId} --> {calleeId}");
            }

            return sb.ToString();
        }

        private string SanitizeMermaid(string text)
            => text.Replace("\"", "'").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}





