using Repo_Into_Graph_DataAccess.Database;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Business;
using Repo_Into_Graph_DataAccess.Models.Method;
using System.Text.Json;

namespace Repo_Into_Graph_Application.Services.Mapper;

public class GraphMapperService
{
    private readonly AnalysisDbContext _context;

    public GraphMapperService(AnalysisDbContext context)
    {
        _context = context;
    }

    public async Task ProcessAndMapGraphAsync(Guid analysisRunId, string businessJsonPath)
    {
        if (!File.Exists(businessJsonPath))
        {
            throw new FileNotFoundException($"Khong tim thay file cau hinh tai: {businessJsonPath}");
        }

        string businessJson = await File.ReadAllTextAsync(businessJsonPath);
        var businessData = JsonSerializer.Deserialize<List<BusinessConfig>>(businessJson);

        if (businessData == null || !businessData.Any()) return;

        var businessRecords = businessData.Select(b => new Business
        {
            Id = Guid.NewGuid(),
            AnalysisRunId = analysisRunId,
            BusinessName = b.business_name.Trim(),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _context.Set<Business>().AddRangeAsync(businessRecords);
        await _context.SaveChangesAsync();

        var methodSourcesInRam = await _context.Set<MethodSourceRecord>()
            .Where(m => m.AnalysisRunId == analysisRunId)
            .ToListAsync();

        var callGraphEdgesInRam = await _context.Set<CallGraphEdge>()
            .Where(e => e.AnalysisRunId == analysisRunId)
            .ToListAsync();

        var graphLookup = callGraphEdgesInRam.ToLookup(
            e => $"{e.CallerClass.Trim().ToLower()}.{e.CallerMethod.Trim().ToLower()}"
        );

        var methodIdLookup = methodSourcesInRam.ToLookup(
            m => $"{m.ClassName.Trim().ToLower()}.{m.MethodName.Trim().ToLower()}",
            m => m.Id
        );

        var implementationLookup = BuildImplementationLookup(methodSourcesInRam);

        var businessLookup = businessRecords.ToDictionary(
            b => b.BusinessName.ToLower(),
            b => b.Id
        );

        var mappingsToInsert = new List<BusinessMethodMapping>();

        foreach (var bizConfig in businessData)
        {
            string cleanBizName = bizConfig.business_name.Trim().ToLower();
            if (!businessLookup.TryGetValue(cleanBizName, out Guid currentBusinessId)) continue;

            var visitedMethodIds = new HashSet<Guid>();

            foreach (var api in bizConfig.apis)
            {
                if (string.IsNullOrEmpty(api.controller) || string.IsNullOrEmpty(api.method)) continue;

                var methodParts = api.method.Trim().Split(' ');
                string methodName = methodParts.Length > 1 ? methodParts[1].Trim() : methodParts[0].Trim();
                string controllerName = api.controller.Trim();

                string rootKey = $"{controllerName.ToLower()}.{methodName.ToLower()}";

                if (methodIdLookup.Contains(rootKey))
                {
                    foreach (var id in methodIdLookup[rootKey])
                        visitedMethodIds.Add(id);
                }

                FindAllMethodsInSubTree(rootKey, graphLookup, methodIdLookup, implementationLookup, visitedMethodIds);
            }

            foreach (var methodSourceId in visitedMethodIds)
            {
                mappingsToInsert.Add(new BusinessMethodMapping
                {
                    Id = Guid.NewGuid(),
                    BusinessId = currentBusinessId,
                    MethodSourceId = methodSourceId,
                    MappedAt = DateTime.UtcNow
                });
            }
        }

        if (mappingsToInsert.Any())
        {
            await _context.Set<BusinessMethodMapping>().AddRangeAsync(mappingsToInsert);
            await _context.SaveChangesAsync();
        }
    }

    private void FindAllMethodsInSubTree(
        string rootKey,
        ILookup<string, CallGraphEdge> graphLookup,
        ILookup<string, Guid> methodIdLookup,
        ILookup<string, string> implementationLookup,
        HashSet<Guid> visitedMethodIds)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootKey);
        var visitedKeys = new HashSet<string> { rootKey };

        while (queue.Count > 0)
        {
            var currentKey = queue.Dequeue();
            if (graphLookup.Contains(currentKey))
            {
                foreach (var edge in graphLookup[currentKey])
                {
                    string calleeKey = $"{edge.CalleeClass.Trim().ToLower()}.{edge.CalleeMethod.Trim().ToLower()}";
                    ProcessNewNode(calleeKey, queue, visitedKeys, methodIdLookup, visitedMethodIds);
                    if (implementationLookup.Contains(calleeKey))
                    {
                        foreach (var concreteKey in implementationLookup[calleeKey])
                            ProcessNewNode(concreteKey, queue, visitedKeys, methodIdLookup, visitedMethodIds);
                    }
                }
            }
        }
    }

    private void ProcessNewNode(
        string nodeKey,
        Queue<string> queue,
        HashSet<string> visitedKeys,
        ILookup<string, Guid> methodIdLookup,
        HashSet<Guid> visitedMethodIds)
    {
        if (!visitedKeys.Contains(nodeKey))
        {
            visitedKeys.Add(nodeKey);
            if (methodIdLookup.Contains(nodeKey))
            {
                foreach (var id in methodIdLookup[nodeKey])
                    visitedMethodIds.Add(id);
            }
            queue.Enqueue(nodeKey);
        }
    }

    private ILookup<string, string> BuildImplementationLookup(List<MethodSourceRecord> methods)
    {
        var mappings = new List<(string InterfaceKey, string ConcreteKey)>();
        var groupedByMethod = methods.GroupBy(m => m.MethodName.Trim().ToLower());

        foreach (var group in groupedByMethod)
        {
            string methodName = group.Key;
            var interfaces = group.Where(m => m.ClassName.Trim().StartsWith("I") && m.ClassName.Trim().Length > 1).ToList();
            var concretes = group.Where(m => !m.ClassName.Trim().StartsWith("I")).ToList();

            foreach (var iface in interfaces)
            {
                string cleanIfaceName = iface.ClassName.Trim().Substring(1).ToLower();
                foreach (var concrete in concretes)
                {
                    string concreteName = concrete.ClassName.Trim().ToLower();
                    if (concreteName.Contains(cleanIfaceName) || concreteName.Replace("impl", "").Contains(cleanIfaceName))
                    {
                        string ifaceKey = $"{iface.ClassName.Trim().ToLower()}.{methodName}";
                        string concreteKey = $"{concrete.ClassName.Trim().ToLower()}.{methodName}";
                        mappings.Add((ifaceKey, concreteKey));
                    }
                }
            }
        }

        return mappings.ToLookup(x => x.InterfaceKey, x => x.ConcreteKey);
    }
}