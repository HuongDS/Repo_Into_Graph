using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph.Data;
using Repo_Into_Graph.Repo_Into_Graph.Data;
using Repo_Into_Graph.Repo_Into_Graph.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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

      
        // Load all call graph edges for this run
        var callGraphEdges = await _context.Set<CallGraphEdgeRecord>()
            .Where(e => e.AnalysisRunId == analysisRunId)
            .ToListAsync();

        var methodLookup = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in methodSourcesInRam)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(m.DisplayName))
            {
                keys.Add(m.DisplayName.Trim());
            }

            if (!string.IsNullOrEmpty(m.HttpVerb))
            {
                keys.Add($"{m.HttpVerb} {m.MethodName}".Trim());
            }

            keys.Add(m.MethodName.Trim());

            foreach (var key in keys)
            {
                if (!methodLookup.TryGetValue(key, out var list))
                {
                    list = new List<Guid>();
                    methodLookup[key] = list;
                }
                list.Add(m.Id);
            }
        }

        var methodBySignature = methodSourcesInRam
            .GroupBy(m => $"{m.ClassName}::{m.MethodName}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var edgesByCaller = callGraphEdges
            .GroupBy(e => $"{e.CallerClass}::{e.CallerMethod}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var featureLookup = featureRecords.ToDictionary(
            f => f.FeatureName.ToLower(),
            f => f.Id
        );

        var mappingsToInsert = new List<FeatureMethodMapping>();

        foreach (var featConfig in featuresData)
        {
            string cleanFeatName = featConfig.feature_name.Trim().ToLower();

            if (!featureLookup.TryGetValue(cleanFeatName, out Guid currentFeatureId)) continue;

            var visitedMethodIds = new HashSet<Guid>();
            var queue = new Queue<MethodSourceRecord>();

            foreach (var apiStr in featConfig.apis)
            {
                string cleanApiStr = apiStr.Trim();

                if (methodLookup.TryGetValue(cleanApiStr, out var methodSourceIds))
                {
                    foreach (var methodSourceId in methodSourceIds)
                    {
                        var startMethod = methodSourcesInRam.FirstOrDefault(m => m.Id == methodSourceId);
                        if (startMethod != null && visitedMethodIds.Add(startMethod.Id))
                        {
                            queue.Enqueue(startMethod);
                        }
                    }
                }
            }

            // Perform Breadth-First Search (BFS) to find transitively called methods in the call graph
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var callerKey = $"{current.ClassName}::{current.MethodName}";

                if (edgesByCaller.TryGetValue(callerKey, out var edges))
                {
                    foreach (var edge in edges)
                    {
                        var calleeKey = $"{edge.CalleeClass}::{edge.CalleeMethod}";
                        if (methodBySignature.TryGetValue(calleeKey, out var callees))
                        {
                            foreach (var callee in callees)
                            {
                                if (visitedMethodIds.Add(callee.Id))
                                {
                                    queue.Enqueue(callee);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var methodSourceId in visitedMethodIds)
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
}