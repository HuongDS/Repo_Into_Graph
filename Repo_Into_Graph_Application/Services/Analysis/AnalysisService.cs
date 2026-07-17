using Repo_Into_Graph_DataAccess.Database;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_Application.Dtos.Analysis;
using Repo_Into_Graph_Application.Exceptions;
using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Models.Method;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_Application.Services.DataFlowParser;
using Repo_Into_Graph_Application.Services.GitService;
using Repo_Into_Graph_Application.Services.Mapper;
using Repo_Into_Graph_Application.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Analysis
{
    public class AnalysisService : IAnalysisService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAnalysisRunRepository _analysisRunRepository;
        private readonly IMethodSourceRepository _methodSourceRepository;
        private readonly IFeatureRepository _featureRepository;
        private readonly IBusinessRepository _businessRepository;
        private readonly ICallGraphEdgeRepository _callGraphEdgeRepository;
        private readonly GraphMapperService _graphMapper;
        private readonly IGitService _gitService;

        private readonly BusinessFlowParser _businessFlowParser;
        private readonly DataFlowParseService _dataFlowParser;
        private readonly BusinessCallDataFlowGenerator _businessCallDataFlowGenerator;

        public AnalysisService(
            IUnitOfWork unitOfWork,
            IAnalysisRunRepository analysisRunRepository,
            IMethodSourceRepository methodSourceRepository,
            IFeatureRepository featureRepository,
            IBusinessRepository businessRepository,
            ICallGraphEdgeRepository callGraphEdgeRepository,
            GraphMapperService graphMapper,
            IGitService gitService,
            BusinessFlowParser businessFlowParser,
            BusinessCallDataFlowGenerator businessCallDataFlowGenerator,
            DataFlowParseService dataFlowParser)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _analysisRunRepository = analysisRunRepository ?? throw new ArgumentNullException(nameof(analysisRunRepository));
            _methodSourceRepository = methodSourceRepository ?? throw new ArgumentNullException(nameof(methodSourceRepository));
            _featureRepository = featureRepository ?? throw new ArgumentNullException(nameof(featureRepository));
            _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
            _callGraphEdgeRepository = callGraphEdgeRepository ?? throw new ArgumentNullException(nameof(callGraphEdgeRepository));
            _graphMapper = graphMapper ?? throw new ArgumentNullException(nameof(graphMapper));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _businessFlowParser = businessFlowParser ?? throw new ArgumentNullException(nameof(businessFlowParser));
            _dataFlowParser = dataFlowParser ?? throw new ArgumentNullException(nameof(dataFlowParser));
            _businessCallDataFlowGenerator = businessCallDataFlowGenerator ?? throw new ArgumentNullException(nameof(businessCallDataFlowGenerator));
        }

        public async Task<AnalysisResponseDto> AnalyzeRepositoryAsync(string repositoryPath, string? outputDir)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
                throw new BadRequestException("Đường dẫn repository hoặc URL git không được để trống.");

            string trimmedRepoPath = repositoryPath.Trim('"', ' ');
            string targetOutputDir = string.IsNullOrWhiteSpace(outputDir) ? "./output" : outputDir.Trim('"', ' ');

            bool isGitUrl = _gitService.IsGitUrl(trimmedRepoPath);
            string targetPath = trimmedRepoPath;
            bool isTempDirectory = false;

            if (isGitUrl)
            {
                targetPath = await _gitService.CloneRepositoryAsync(trimmedRepoPath);
                isTempDirectory = true;
            }
            else if (!Directory.Exists(trimmedRepoPath))
            {
                throw new NotFoundException($"Thư mục local không tồn tại: {trimmedRepoPath}");
            }


            try
            {
                Directory.CreateDirectory(targetOutputDir);
                var analyzer = new CodeAnalyzer(targetPath);
                var result = await analyzer.AnalyzeAsync();

                var existingRuns = await _analysisRunRepository
                    .FindAsync(r => r.RepositoryPath.ToLower() == trimmedRepoPath.ToLower());

                if (existingRuns.Any())
                {
                    _analysisRunRepository.DeleteRange(existingRuns);
                    _methodSourceRepository.DeleteRange(existingRuns.SelectMany(r => r.MethodSources));
                    _featureRepository.DeleteRange(existingRuns.SelectMany(r => r.Features));
                    _businessRepository.DeleteRange(existingRuns.SelectMany(r => r.Businesses));
                    _callGraphEdgeRepository.DeleteRange(existingRuns.SelectMany(r => r.CallGraphEdges));
                }

                // Tạo AnalysisRun mới
                var analysisRun = new AnalysisRun
                {
                    Id = Guid.NewGuid(),
                    RepositoryPath = trimmedRepoPath,
                    CreatedAt = DateTime.UtcNow,
                    CallGraphEdges = result.CallGraph.Select(edge => new CallGraphEdge
                    {
                        Id = Guid.NewGuid(),
                        CallerClass = edge.CallerClass,
                        CallerMethod = edge.CallerMethod,
                        CalleeClass = edge.CalleeClass,
                        CalleeMethod = edge.CalleeMethod,
                        CreatedAt = DateTime.UtcNow
                    }).ToList(),
                    MethodSources = result.MethodSources.Select(source => new MethodSourceRecord
                    {
                        Id = Guid.NewGuid(),
                        ClassName = source.ClassName,
                        MethodName = source.MethodName,
                        SourceCode = source.SourceCode,
                        Type = source.Type,
                        CreatedAt = DateTime.UtcNow
                    }).ToList()
                };

                await _analysisRunRepository.AddAsync(analysisRun);
                var allIntraEdges = new List<DataFlowEdge>();
                foreach (var source in analysisRun.MethodSources)
                {
                    var methodDataFlows = _dataFlowParser.ParseIntraMethodDataFlow(analysisRun.Id, source.ClassName, source.MethodName, source.SourceCode);
                    allIntraEdges.AddRange(methodDataFlows);
                }
                //if(allIntraEdges.Any())
                //{
                //    await _context.DataFlowEdges.AddRangeAsync(allIntraEdges);
                //    await _context.SaveChangesAsync();
                //}
                // Phan tich va luu Features (luong xu ly)
                var features = _businessFlowParser.ParseBusinessFlows(analysisRun.Id, analysisRun.CallGraphEdges);
                if (features.Any())
                {
                    var methodSourcesList = analysisRun.MethodSources.ToList();
                    foreach (var flow in features)
                    {
                        flow.DataFlowMermaidGraph = _businessCallDataFlowGenerator.GenerateCallDataFlow(flow, methodSourcesList, allIntraEdges);
                    }
                    await _featureRepository.AddRangeAsync(features);
                }

                await _unitOfWork.SaveChangesAsync();

                // Thuc hien anh xa do thi voi Business (doc tu template_business.json)
                // Uu tien doc template_business.json tu ben trong repository duoc phan tich,
                // neu khong co thi fallback ve file local cua server
                string businessJsonPath = Path.Combine(targetPath, "template_business.json");
                Console.WriteLine($"[DEBUG] Đang tìm kiếm file template tại: {businessJsonPath}");
                
                if (!File.Exists(businessJsonPath))
                {
                    Console.WriteLine($"[DEBUG] Không tìm thấy file ở targetPath. Đang tìm file dự phòng...");
                    businessJsonPath = "template_business.json";
                    Console.WriteLine("Ahihihihi1");
                }


                if (File.Exists(businessJsonPath))
                {
                    await _graphMapper.ProcessAndMapGraphAsync(analysisRun.Id, businessJsonPath);

                }


                // Xuất file output
                var outputJsonPath = Path.Combine(targetOutputDir, "output_graph.json");
                await OutputWriter.WriteJsonAsync(outputJsonPath, result);
                await OutputWriter.WriteMermaidAsync(targetOutputDir, result);
                await OutputWriter.WriteHtmlAsync(targetOutputDir, result);

                return new AnalysisResponseDto
                {
                    Message = "Phân tích và lưu vào cơ sở dữ liệu thành công!",
                    AnalysisRunId = analysisRun.Id,
                    EdgesCount = result.CallGraph.Count,
                    MethodsCount = result.MethodSources.Count
                };
            }
            finally
            {
                if (isTempDirectory)
                {
                    _gitService.DeleteClonedRepository(targetPath);
                }
            }
        }
    }
}





