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
    private readonly IBusinessRepository _businessRepository;
    private readonly IMethodSourceRepository _methodSourceRepository;
    private readonly ICallGraphEdgeRepository _callGraphEdgeRepository;
    private readonly IFeatureRepository _featureRepository;
    private readonly IFeatureBusinessMappingRepository _featureBusinessMappingRepository;
    private readonly IFeatureMethodMappingRepository _featureMethodMappingRepository;

    public GraphMapperService(
        IUnitOfWork unitOfWork,
        IBusinessRepository businessRepository,
        IMethodSourceRepository methodSourceRepository,
        ICallGraphEdgeRepository callGraphEdgeRepository,
        IFeatureRepository featureRepository,
        IFeatureBusinessMappingRepository featureBusinessMappingRepository,
        IFeatureMethodMappingRepository featureMethodMappingRepository)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
        _methodSourceRepository = methodSourceRepository ?? throw new ArgumentNullException(nameof(methodSourceRepository));
        _callGraphEdgeRepository = callGraphEdgeRepository ?? throw new ArgumentNullException(nameof(callGraphEdgeRepository));
        _featureRepository = featureRepository ?? throw new ArgumentNullException(nameof(featureRepository));
        _featureBusinessMappingRepository = featureBusinessMappingRepository ?? throw new ArgumentNullException(nameof(featureBusinessMappingRepository));
        _featureMethodMappingRepository = featureMethodMappingRepository ?? throw new ArgumentNullException(nameof(featureMethodMappingRepository));
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

        await _businessRepository.AddRangeAsync(businessRecords);

        var methodSourcesInRam = await _methodSourceRepository.GetByAnalysisRunIdAsync(analysisRunId);

        var callGraphEdgesInRam = await _callGraphEdgeRepository.GetByAnalysisRunIdAsync(analysisRunId);

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

        var mappingsToInsert = new List<FeatureMethodMapping>();
        var featureBusinessMappingsToInsert = new List<FeatureBusinessMapping>();

        var featuresInRam = await _featureRepository.GetByAnalysisRunIdAsync(analysisRunId);
            
        var mappedFeatureIds = new HashSet<Guid>();

        foreach (var bizConfig in businessData)
        {
            string cleanBizName = bizConfig.business_name.Trim().ToLower();
            if (!businessLookup.TryGetValue(cleanBizName, out Guid currentBusinessId)) continue;

            foreach (var api in bizConfig.apis)
            {
                if (string.IsNullOrEmpty(api.controller) || string.IsNullOrEmpty(api.method)) continue;

                var methodParts = api.method.Trim().Split(' ');
                string methodName = methodParts.Length > 1 ? methodParts[1].Trim() : methodParts[0].Trim();
                string controllerName = api.controller.Trim();

                string rootKey = $"{controllerName.ToLower()}.{methodName.ToLower()}";

                // Map Business to Feature
                var matchedFeature = featuresInRam.FirstOrDefault(f => f.EntryPoint.ToLower().EndsWith(rootKey));
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
            await _featureBusinessMappingRepository.AddRangeAsync(featureBusinessMappingsToInsert);
        }

        if (mappingsToInsert.Any())
        {
            await _featureMethodMappingRepository.AddRangeAsync(mappingsToInsert);
        }
        
        await _unitOfWork.SaveChangesAsync();
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