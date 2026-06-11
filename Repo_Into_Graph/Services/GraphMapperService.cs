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

        // 1. Tạo Lookup đồ thị cuộc gọi thông thường (Dùng Tên Gốc, KHÔNG dùng hàm GetNormalizedKey cũ để tránh mất Prefix)
        var graphLookup = callGraphEdgesInRam.ToLookup(
            e => $"{e.CallerClass.Trim().ToLower()}.{e.CallerMethod.Trim().ToLower()}"
        );

        // Map từ Class.Method gốc ra Method ID để lưu xuống DB
        var methodIdLookup = methodSourcesInRam.ToLookup(
            m => $"{m.ClassName.Trim().ToLower()}.{m.MethodName.Trim().ToLower()}",
            m => m.Id
        );

        // 2. [CỐT LÕI] Tự động xây dựng bản đồ Interface -> Các Class thực thi dựa trên MethodSource
        // Giả định: Công cụ quét của ông lưu ClassName của Interface là "IAuctionService" và Class thực thi là "VipAuctionService"
        // Ở đây mình group theo tên Method và tìm các cặp có quan hệ Interface - Implementation.
        // Cách sạch nhất: Tìm các class thực thi dựa vào logic tên (Xóa chữ I đầu) hoặc nếu trong DB MethodSource có trường "IsInterface" thì càng tốt.
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

                // Điểm bắt đầu chính xác từ Controller gốc (Ví dụ: auctioncontroller.getauction)
                string rootKey = $"{controllerName.ToLower()}.{methodName.ToLower()}";

                if (methodIdLookup.Contains(rootKey))
                {
                    foreach (var id in methodIdLookup[rootKey])
                    {
                        visitedMethodIdsForThisFeature.Add(id);
                    }
                }

                // Gọi hàm loang cây có hỗ trợ đa nhánh đa hình
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
        ILookup<string, CallGraphEdgeRecord> graphLookup,
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

            // BƯỚC 1: Dò các cạnh gọi thông thường từ Node hiện tại
            if (graphLookup.Contains(currentKey))
            {
                foreach (var edge in graphLookup[currentKey])
                {
                    string calleeKey = $"{edge.CalleeClass.Trim().ToLower()}.{edge.CalleeMethod.Trim().ToLower()}";

                    // Đẩy nhánh thông thường vào queue
                    ProcessNewNode(calleeKey, queue, visitedKeys, methodIdLookup, visitedMethodIds);

                    // BƯỚC 2: [ĐA HÌNH] Nếu thằng được gọi (Callee) là một Interface, check xem có các Class thực thi nào không
                    if (implementationLookup.Contains(calleeKey))
                    {
                        foreach (var concreteClassKey in implementationLookup[calleeKey])
                        {
                            // Đẩy TẤT CẢ các Class thực thi (VipAuctionService, NormalAuctionService...) vào Queue để loang song song
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
    /// Hàm tự động quét và ghép cặp Interface -> Danh sách Class thực thi
    /// </summary>
    private ILookup<string, string> BuildImplementationLookup(List<MethodSourceRecord> methods)
    {
        var mappings = new List<(string InterfaceKey, string ConcreteKey)>();

        // Group các hàm có cùng tên method để tìm kiếm mối quan hệ trùng signature
        var groupedByMethod = methods.GroupBy(m => m.MethodName.Trim().ToLower());

        foreach (var group in groupedByMethod)
        {
            string methodName = group.Key;

            // Tìm các bản ghi nghi ngờ là Interface (Bắt đầu bằng chữ I)
            var interfaces = group.Where(m => m.ClassName.Trim().StartsWith("I") && m.ClassName.Trim().Length > 1).ToList();
            var concretes = group.Where(m => !m.ClassName.Trim().StartsWith("I")).ToList();

            foreach (var @interface in interfaces)
            {
                string cleanInterfaceName = @interface.ClassName.Trim().Substring(1).ToLower(); // Xóa chữ I đầu

                foreach (var concrete in concretes)
                {
                    string concreteClassName = concrete.ClassName.Trim().ToLower();

                    // Nếu tên class thực thi chứa cụm từ của interface (ví dụ: vipauctionservice chứa auctionservice)
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