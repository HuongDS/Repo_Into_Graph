using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Repo_Into_Graph_Application.Services.DataFlowParser
{
    public class BusinessFlowParser
    {
        // Hậu tố tên class thường gặp cho "điểm vào" của một luồng nghiệp vụ, tùy kiến trúc:
        // - MVC/Web API cổ điển: Controller
        // - CQRS (MediatR-style): Handler (Command/Query/Event handler)
        // - Clean Architecture/DDD: UseCase, Interactor, ApplicationService/AppService
        // - Minimal API / FastEndpoints / Carter: Endpoint
        // - Message-driven (RabbitMQ/MassTransit/Kafka consumer, background worker): Consumer
        private static readonly string[] _entryPointSuffixes =
        {
            "Controller", "Handler", "UseCase", "Interactor",
            "ApplicationService", "AppService", "Endpoint", "Consumer"
        };

        public List<Feature> ParseBusinessFlows(Guid analysisRunId, List<CallGraphEdge> edges)
        {
            var features = new List<Feature>();

            if (edges == null || !edges.Any())
                return features;

            // 1. Group edges by Caller (Class::Method)
            var graphLookup = edges.ToLookup(
                e => $"{e.CallerClass.Trim().ToLower()}::{e.CallerMethod.Trim().ToLower()}"
            );

            // 2. Identify entry points theo naming convention phổ biến (Controller/Handler/UseCase/...)
            var controllerMethods = edges
                .Where(e => _entryPointSuffixes.Any(suffix => e.CallerClass.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                .Select(e => new { Class = e.CallerClass, Method = e.CallerMethod })
                .Distinct()
                .ToList();

            // 3. Fallback: nếu không có class nào khớp naming convention ở trên (kiến trúc đặt tên
            // hoàn toàn tùy biến), coi các "gốc" của call graph - method không bị ai trong repo gọi tới -
            // là điểm vào tiềm năng của luồng nghiệp vụ.
            if (!controllerMethods.Any())
            {
                var calleeKeys = new HashSet<string>(
                    edges.Select(e => $"{e.CalleeClass.Trim().ToLower()}::{e.CalleeMethod.Trim().ToLower()}"));

                controllerMethods = edges
                    .Select(e => new { Class = e.CallerClass, Method = e.CallerMethod })
                    .Distinct()
                    .Where(e => !calleeKeys.Contains($"{e.Class.Trim().ToLower()}::{e.Method.Trim().ToLower()}"))
                    .ToList();
            }

            // 4. Loại bỏ các entry point "con": khi thêm Handler/UseCase/... vào danh sách entry point (bước 2),
            // một class như CommandHandler vừa là bước bên trong flow của Controller (vì Controller gọi tới nó
            // qua Send/Publish hoặc gọi trực tiếp), vừa tự nó cũng khớp naming convention entry point.
            // Nếu không lọc, nó sẽ sinh thêm 1 Feature trùng lặp/chồng lấn với Feature của Controller.
            // => chỉ giữ lại các entry point không bị "with tới" (reachable) từ 1 entry point khác trong danh sách.
            // Với pattern Controller/Service/Repository thuần túy, danh sách entry point chỉ gồm các Controller
            // độc lập nên bước lọc này gần như không có tác động (Controller hiếm khi gọi trực tiếp 1 Controller khác).
            if (controllerMethods.Count > 1)
            {
                bool IsReachable(string fromKey, string targetKey)
                {
                    var visited = new HashSet<string> { fromKey };
                    var queue = new Queue<string>();
                    queue.Enqueue(fromKey);

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        if (!graphLookup.Contains(current)) continue;

                        foreach (var edge in graphLookup[current])
                        {
                            var calleeKey = $"{edge.CalleeClass.Trim().ToLower()}::{edge.CalleeMethod.Trim().ToLower()}";
                            if (calleeKey == targetKey) return true;
                            if (visited.Add(calleeKey))
                                queue.Enqueue(calleeKey);
                        }
                    }

                    return false;
                }

                var keyed = controllerMethods
                    .Select(e => new { e.Class, e.Method, Key = $"{e.Class.Trim().ToLower()}::{e.Method.Trim().ToLower()}" })
                    .ToList();

                controllerMethods = keyed
                    .Where(candidate => !keyed.Any(other => other.Key != candidate.Key && IsReachable(other.Key, candidate.Key)))
                    .Select(e => new { e.Class, e.Method })
                    .ToList();
            }

            foreach (var start in controllerMethods)
            {
                var flowId = Guid.NewGuid();
                var flowName = $"{start.Class}.{start.Method}";
                var entryPoint = $"{start.Class}.{start.Method}";

                var steps = new List<FeatureStep>();
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

                            steps.Add(new FeatureStep
                            {
                                Id = Guid.NewGuid(),
                                FeatureId = flowId,
                                CallerClass = edge.CallerClass,
                                CallerMethod = edge.CallerMethod,
                                CalleeClass = edge.CalleeClass,
                                CalleeMethod = edge.CalleeMethod,
                                StepOrder = order++,
                                CreatedAt = DateTime.UtcNow,
                                ConditionContext = edge.ConditionContext
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
                    features.Add(new Feature
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

            return features;
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





