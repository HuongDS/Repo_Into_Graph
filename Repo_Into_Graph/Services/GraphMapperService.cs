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

      
        var methodLookup = methodSourcesInRam.ToLookup(
            m => m.MethodName.Trim().ToLower(),
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

            foreach (var apiStr in featConfig.apis)
            {
                var parts = apiStr.Trim().Split(' ');
                string methodNameFromApi = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
                string cleanMethodName = methodNameFromApi.ToLower();

                if (methodLookup.Contains(cleanMethodName))
                {
                    foreach (var methodSourceId in methodLookup[cleanMethodName])
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
            }
        }

        if (mappingsToInsert.Any())
        {
            await _context.Set<FeatureMethodMapping>().AddRangeAsync(mappingsToInsert);
            await _context.SaveChangesAsync();
        }
    }
}