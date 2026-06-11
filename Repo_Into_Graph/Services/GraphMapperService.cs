using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph.Data;
using Repo_Into_Graph.Repo_Into_Graph.Data;
using Repo_Into_Graph.Repo_Into_Graph.Models;
using System.Text.Json;

namespace Repo_Into_Graph.Services;

public class GraphMapperService
{
    private readonly AnalysisDbContext _context;

    public GraphMapperService(AnalysisDbContext context)
    {
        _context = context;
    }

    public async Task ProcessAndMapGraphAsync(Guid analysisRunId, string featuresJsonPath)
    {
        if (!File.Exists(featuresJsonPath))
        {
            throw new FileNotFoundException($"Không tìm thấy file cấu hình tại: {featuresJsonPath}");
        }

        string featuresJson = await File.ReadAllTextAsync(featuresJsonPath);

        var featuresData = JsonSerializer.Deserialize<List<FeatureConfig>>(featuresJson);

        if (featuresData == null || !featuresData.Any()) return;

        var featureRecords = featuresData.Select(f => new FeatureRecord
        {
            Id = Guid.NewGuid(),
            AnalysisRunId = analysisRunId,
            FeatureName = f.feature_name.Trim(),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _context.Set<FeatureRecord>().AddRangeAsync(featureRecords);
        await _context.SaveChangesAsync();

        var methodSourcesInRam = await _context.Set<MethodSourceRecord>()
            .Where(m => m.AnalysisRunId == analysisRunId)
            .ToListAsync();

        var callGraphEdgesInRam = await _context.Set<CallGraphEdgeRecord>()
            .Where(e => e.AnalysisRunId == analysisRunId)
            .ToListAsync();

        var graphLookup = callGraphEdgesInRam.ToLookup(
            e => GetNormalizedKey(e.CallerClass, e.CallerMethod)
        );

        var methodIdLookup = methodSourcesInRam.ToLookup(
            m => GetNormalizedKey(m.ClassName, m.MethodName),
            m => m.Id
        );

        var featureLookup = featureRecords.ToDictionary(
            f => f.FeatureName.ToLower(),
            f => f.Id
        );

        var mappingsToInsert = new List<FeatureMethodMapping>();

        foreach (var featConfig in featuresData)
        {
            string cleanFeatName = featConfig.feature_name.Trim().ToLower();
            if (!featureLookup.TryGetValue(cleanFeatName, out Guid currentFeatureId)) continue;

            var visitedMethodIdsForThisFeature = new HashSet<Guid>();

            foreach (var api in featConfig.apis)
            {
                if (string.IsNullOrEmpty(api.controller) || string.IsNullOrEmpty(api.method)) continue;

                var methodParts = api.method.Trim().Split(' ');
                string methodName = methodParts.Length > 1 ? methodParts[1].Trim() : methodParts[0].Trim();
                string controllerName = api.controller.Trim();

                string rootKey = GetNormalizedKey(controllerName, methodName);

                if (methodIdLookup.Contains(rootKey))
                {
                    foreach (var id in methodIdLookup[rootKey])
                    {
                        visitedMethodIdsForThisFeature.Add(id);
                    }
                }

                FindAllMethodsInSubTree(rootKey, graphLookup, methodIdLookup, visitedMethodIdsForThisFeature);
            }

            foreach (var methodSourceId in visitedMethodIdsForThisFeature)
            {
                mappingsToInsert.Add(new FeatureMethodMapping
                {
                    Id = Guid.NewGuid(),
                    FeatureId = currentFeatureId,
                    MethodSourceId = methodSourceId,
                    MappedAt = DateTime.UtcNow
                });
            }
        }

        if (mappingsToInsert.Any())
        {
            await _context.Set<FeatureMethodMapping>().AddRangeAsync(mappingsToInsert);
            await _context.SaveChangesAsync();
        }
    }

    private void FindAllMethodsInSubTree(
        string rootKey,
        ILookup<string, CallGraphEdgeRecord> graphLookup,
        ILookup<string, Guid> methodIdLookup,
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
                    string calleeKey = GetNormalizedKey(edge.CalleeClass, edge.CalleeMethod);

                    if (!visitedKeys.Contains(calleeKey))
                    {
                        visitedKeys.Add(calleeKey);

                        if (methodIdLookup.Contains(calleeKey))
                        {
                            foreach (var calleeId in methodIdLookup[calleeKey])
                            {
                                visitedMethodIds.Add(calleeId);
                            }
                        }

                        queue.Enqueue(calleeKey);
                    }
                }
            }
        }
    }

    private static string GetNormalizedKey(string className, string methodName)
    {
        string c = className.Trim().ToLower()
            .Replace("impl", "")
            .Replace("service", "")
            .Replace("repository", "")
            .Replace("controller", "");

        if (c.StartsWith("i") && c.Length > 1)
        {
            c = c[1..];
        }

        string m = methodName.Trim().ToLower();
        if (m.EndsWith("async"))
        {
            m = m[..^5];
        }

        return $"{c}.{m}";
    }
}