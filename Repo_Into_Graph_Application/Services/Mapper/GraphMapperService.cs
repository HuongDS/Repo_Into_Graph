using Repo_Into_Graph_DataAccess.Database;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Feature;
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

    public async Task ProcessAndMapGraphAsync(Guid analysisRunId, string featuresJsonPath)
    {
        if (!File.Exists(featuresJsonPath))
        {
            throw new FileNotFoundException($"Không tìm th?y file c?u hình t?i: {featuresJsonPath}");
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

        var callGraphEdgesInRam = await _context.Set<CallGraphEdge>()
            .Where(e => e.AnalysisRunId == analysisRunId)
            .ToListAsync();

        // 1. T?o Lookup d? th? cu?c g?i thông thu?ng (Dùng Tên G?c, KHÔNG dùng hàm GetNormalizedKey cu d? tránh m?t Prefix)
        var graphLookup = callGraphEdgesInRam.ToLookup(
            e => $"{e.CallerClass.Trim().ToLower()}.{e.CallerMethod.Trim().ToLower()}"
        );

        // Map t? Class.Method g?c ra Method ID d? luu xu?ng DB
        var methodIdLookup = methodSourcesInRam.ToLookup(
            m => $"{m.ClassName.Trim().ToLower()}.{m.MethodName.Trim().ToLower()}",
            m => m.Id
        );

        // 2. [C?T LÕI] T? d?ng xây d?ng b?n d? Interface -> Các Class th?c thi d?a trên MethodSource
        // Gi? d?nh: Công c? quét c?a ông luu ClassName c?a Interface là "IAuctionService" và Class th?c thi là "VipAuctionService"
        // ? dây mình group theo tên Method và tìm các c?p có quan h? Interface - Implementation.
        // Cách s?ch nh?t: Tìm các class th?c thi d?a vào logic tên (Xóa ch? I d?u) ho?c n?u trong DB MethodSource có tru?ng "IsInterface" thì càng t?t.
        var implementationLookup = BuildImplementationLookup(methodSourcesInRam);

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

                // Ði?m b?t d?u chính xác t? Controller g?c (Ví d?: auctioncontroller.getauction)
                string rootKey = $"{controllerName.ToLower()}.{methodName.ToLower()}";

                if (methodIdLookup.Contains(rootKey))
                {
                    foreach (var id in methodIdLookup[rootKey])
                    {
                        visitedMethodIdsForThisFeature.Add(id);
                    }
                }

                // G?i hàm loang cây có h? tr? da nhánh da hình
                FindAllMethodsInSubTree(rootKey, graphLookup, methodIdLookup, implementationLookup, visitedMethodIdsForThisFeature);
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

            // BU?C 1: Dò các c?nh g?i thông thu?ng t? Node hi?n t?i
            if (graphLookup.Contains(currentKey))
            {
                foreach (var edge in graphLookup[currentKey])
                {
                    string calleeKey = $"{edge.CalleeClass.Trim().ToLower()}.{edge.CalleeMethod.Trim().ToLower()}";

                    // Ð?y nhánh thông thu?ng vào queue
                    ProcessNewNode(calleeKey, queue, visitedKeys, methodIdLookup, visitedMethodIds);

                    // BU?C 2: [ÐA HÌNH] N?u th?ng du?c g?i (Callee) là m?t Interface, check xem có các Class th?c thi nào không
                    if (implementationLookup.Contains(calleeKey))
                    {
                        foreach (var concreteClassKey in implementationLookup[calleeKey])
                        {
                            // Ð?y T?T C? các Class th?c thi (VipAuctionService, NormalAuctionService...) vào Queue d? loang song song
                            ProcessNewNode(concreteClassKey, queue, visitedKeys, methodIdLookup, visitedMethodIds);
                        }
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
                {
                    visitedMethodIds.Add(id);
                }
            }
            queue.Enqueue(nodeKey);
        }
    }

    /// <summary>
    /// Hàm t? d?ng quét và ghép c?p Interface -> Danh sách Class th?c thi
    /// </summary>
    private ILookup<string, string> BuildImplementationLookup(List<MethodSourceRecord> methods)
    {
        var mappings = new List<(string InterfaceKey, string ConcreteKey)>();

        // Group các hàm có cùng tên method d? tìm ki?m m?i quan h? trùng signature
        var groupedByMethod = methods.GroupBy(m => m.MethodName.Trim().ToLower());

        foreach (var group in groupedByMethod)
        {
            string methodName = group.Key;

            // Tìm các b?n ghi nghi ng? là Interface (B?t d?u b?ng ch? I)
            var interfaces = group.Where(m => m.ClassName.Trim().StartsWith("I") && m.ClassName.Trim().Length > 1).ToList();
            var concretes = group.Where(m => !m.ClassName.Trim().StartsWith("I")).ToList();

            foreach (var @interface in interfaces)
            {
                string cleanInterfaceName = @interface.ClassName.Trim().Substring(1).ToLower(); // Xóa ch? I d?u

                foreach (var concrete in concretes)
                {
                    string concreteClassName = concrete.ClassName.Trim().ToLower();

                    // N?u tên class th?c thi ch?a c?m t? c?a interface (ví d?: vipauctionservice ch?a auctionservice)
                    if (concreteClassName.Contains(cleanInterfaceName) || concreteClassName.Replace("impl", "").Contains(cleanInterfaceName))
                    {
                        string interfaceKey = $"{@interface.ClassName.Trim().ToLower()}.{methodName}";
                        string concreteKey = $"{concrete.ClassName.Trim().ToLower()}.{methodName}";

                        mappings.Add((interfaceKey, concreteKey));
                    }
                }
            }
        }

        return mappings.ToLookup(x => x.InterfaceKey, x => x.ConcreteKey);
    }
}




