using Repo_Into_Graph_DataAccess.Database;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Business;
using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Models.Method;
using System.Text.Json;

namespace Repo_Into_Graph_Application.Services.Mapper;

public class GraphMapperService
{
    private readonly IUnitOfWork _unitOfWork;

    public GraphMapperService(
        IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

        var businessRecords = businessData.Select(b => new Bussiness
        {
            Id = Guid.NewGuid(),
            AnalysisRunId = analysisRunId,
            BusinessName = b.business_name.Trim(),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _unitOfWork.Businesses.AddRangeAsync(businessRecords);

        var methodSourcesInRam = await _unitOfWork.MethodSources.GetByAnalysisRunIdAsync(analysisRunId);

        var callGraphEdgesInRam = await _unitOfWork.CallGraphEdges.GetByAnalysisRunIdAsync(analysisRunId);

        var graphLookup = callGraphEdgesInRam.ToLookup(
            e => BuildKey(e.CallerClass, e.CallerMethod)
        );

        var methodIdLookup = methodSourcesInRam.ToLookup(
            m => BuildKey(m.ClassName, m.MethodName),
            m => m.Id
        );

        var implementationLookup = BuildImplementationLookup(methodSourcesInRam);

        var businessLookup = businessRecords.ToDictionary(
            b => b.BusinessName.ToLower(),
            b => b.Id
        );

        var mappingsToInsert = new List<FeatureMethodMapping>();
        var featureBusinessMappingsToInsert = new List<FeatureBusinessMapping>();

        var featuresInRam = await _unitOfWork.Features.GetByAnalysisRunIdAsync(analysisRunId);

        var mappedFeatureIds = new HashSet<Guid>();

        foreach (var bizConfig in businessData)
        {
            string cleanBizName = bizConfig.business_name.Trim().ToLower();
            if (!businessLookup.TryGetValue(cleanBizName, out Guid currentBusinessId)) continue;

            foreach (var api in bizConfig.apis)
            {
                if (string.IsNullOrWhiteSpace(api.controller)) continue;

                // Doc ten ham qua ResolveMethodName() de ho tro ca hai schema cua
                // template_business.json ("method": "POST CreateBudget" lan
                // "function": "getAllProvinces" + "http_method": "GET").
                // Truoc day cho nay doc thang api.method, nen moi template viet theo schema
                // thu hai deu bi bo qua toan bo -> khong sinh duoc mapping nao.
                string methodName = api.ResolveMethodName();
                if (string.IsNullOrWhiteSpace(methodName)) continue;

                string controllerName = api.controller.Trim();

                // Roslyn-captured method names almost always keep the "Async" suffix
                // (e.g. CreateBudgetAsync) even when template_business.json only lists
                // the route-facing name (e.g. CreateBudget), so both sides are normalized
                // via BuildKey/NormalizeMethodName before comparing.
                string rootKey = BuildKey(controllerName, methodName);

                // Map Business to Feature. Không chỉ khớp EntryPoint: với CQRS/DDD, class thật sự chứa
                // business logic (Handler/UseCase) thường bị gộp thành 1 step bên trong flow của
                // Controller (do BusinessFlowParser loại các entry point "con" để tránh Feature trùng
                // lặp/chồng lấn), nên "controller" trong template_business.json có thể trỏ tới Handler
                // chứ không phải Controller. Phải tìm trong cả Steps của Feature, không chỉ EntryPoint.
                var matchedFeature = featuresInRam.FirstOrDefault(f => FeatureMatchesNode(f, rootKey));
                if (matchedFeature != null)
                {
                    featureBusinessMappingsToInsert.Add(new FeatureBusinessMapping
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = currentBusinessId,
                        FeatureId = matchedFeature.Id,
                        CreatedAt = DateTime.UtcNow
                    });

                    // If we haven't mapped this feature's methods yet, trace and map them
                    if (!mappedFeatureIds.Contains(matchedFeature.Id))
                    {
                        mappedFeatureIds.Add(matchedFeature.Id);
                        var visitedMethodIds = new HashSet<Guid>();

                        if (methodIdLookup.Contains(rootKey))
                        {
                            foreach (var id in methodIdLookup[rootKey])
                                visitedMethodIds.Add(id);
                        }

                        FindAllMethodsInSubTree(rootKey, graphLookup, methodIdLookup, implementationLookup, visitedMethodIds);

                        foreach (var methodSourceId in visitedMethodIds)
                        {
                            mappingsToInsert.Add(new FeatureMethodMapping
                            {
                                Id = Guid.NewGuid(),
                                FeatureId = matchedFeature.Id,
                                MethodSourceId = methodSourceId,
                                MappedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
        }

        if (featureBusinessMappingsToInsert.Any())
        {
            await _unitOfWork.FeatureBusinessMappings.AddRangeAsync(featureBusinessMappingsToInsert);
        }

        if (mappingsToInsert.Any())
        {
            await _unitOfWork.FeatureMethodMappings.AddRangeAsync(mappingsToInsert);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private static bool FeatureMatchesNode(Feature feature, string rootKey)
    {
        if (StripAsyncSuffix(feature.EntryPoint.Trim().ToLower()).EndsWith(rootKey)) return true;

        return feature.Steps.Any(s =>
            BuildKey(s.CallerClass, s.CallerMethod).EndsWith(rootKey) ||
            BuildKey(s.CalleeClass, s.CalleeMethod).EndsWith(rootKey));
    }

    // Roslyn captures the real method identifier (e.g. "CreateBudgetAsync"), but
    // business/template configs are frequently authored against the route-facing
    // name (e.g. "CreateBudget"). Stripping the "Async" suffix on every key built
    // from source-derived data keeps matching independent of which form is used.
    private static string StripAsyncSuffix(string lowerMethodName)
    {
        const string suffix = "async";
        return lowerMethodName.Length > suffix.Length && lowerMethodName.EndsWith(suffix)
            ? lowerMethodName.Substring(0, lowerMethodName.Length - suffix.Length)
            : lowerMethodName;
    }

    private static string BuildKey(string className, string methodName)
    {
        return $"{className.Trim().ToLower()}.{StripAsyncSuffix(methodName.Trim().ToLower())}";
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
                    string calleeKey = BuildKey(edge.CalleeClass, edge.CalleeMethod);
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

    /// <summary>
    /// Bac cau tu KHOA GIAO DIEN sang KHOA LOP CAI DAT, de FindAllMethodsInSubTree khong
    /// dung lai khi call graph tro toi interface con source code nam o lop implementation.
    ///
    /// Ho tro HAI quy uoc dat ten:
    ///   - .NET  : IDatasetService  -> DatasetService / DatasetServiceImpl   (I dung dau)
    ///   - Java  : DatasetService   -> DatasetServiceImpl                    (hau to Impl)
    ///
    /// Truoc day chi co nhanh .NET. Voi repo Java, canh tu Controller tro toi
    /// "DatasetService.uploadCSVFile" trong khi MethodSources chi co
    /// "DatasetServiceImpl.uploadCSVFile" => khong bac cau duoc => BFS dung ngay sau node
    /// dau tien. Do tren repo test-appJava: 231/241 canh xuat phat tu Controller khong
    /// khop duoc method nao, khien moi nghiep vu chi map ~1 method va prompt gui LLM gan
    /// nhu khong chua ma nguon (Survey: 67 token code tren tong 1296 token prompt).
    /// </summary>
    private ILookup<string, string> BuildImplementationLookup(List<MethodSourceRecord> methods)
    {
        var mappings = new List<(string InterfaceKey, string ConcreteKey)>();

        // --- Quy uoc Java/Spring: <Ten>Impl cai dat interface <Ten> ---
        // Khong doi hoi interface phai ton tai trong MethodSources: interface Java thuong
        // khai bao method khong co tu khoa truy cap nen JavaParser (doi hoi public/protected/
        // private) khong bat duoc, nhung canh trong call graph van tro toi ten interface.
        const string implSuffix = "Impl";
        foreach (var m in methods)
        {
            string cls = m.ClassName.Trim();
            if (cls.Length <= implSuffix.Length) continue;
            if (!cls.EndsWith(implSuffix, StringComparison.OrdinalIgnoreCase)) continue;

            string interfaceName = cls.Substring(0, cls.Length - implSuffix.Length);
            if (string.IsNullOrWhiteSpace(interfaceName)) continue;

            mappings.Add((BuildKey(interfaceName, m.MethodName), BuildKey(cls, m.MethodName)));
        }

        var groupedByMethod = methods.GroupBy(m => StripAsyncSuffix(m.MethodName.Trim().ToLower()));

        foreach (var group in groupedByMethod)
        {
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
                        string ifaceKey = BuildKey(iface.ClassName, iface.MethodName);
                        string concreteKey = BuildKey(concrete.ClassName, concrete.MethodName);
                        mappings.Add((ifaceKey, concreteKey));
                    }
                }
            }
        }

        return mappings.ToLookup(x => x.InterfaceKey, x => x.ConcreteKey);
    }
}