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
        private readonly GraphMapperService _graphMapper;
        private readonly IGitService _gitService;
        private readonly BusinessFlowParser _businessFlowParser;
        private readonly DataFlowParseService _dataFlowParser;
        private readonly BusinessCallDataFlowGenerator _businessCallDataFlowGenerator;

        public AnalysisService(
            IUnitOfWork unitOfWork,
            GraphMapperService graphMapper,
            IGitService gitService,
            BusinessFlowParser businessFlowParser,
            BusinessCallDataFlowGenerator businessCallDataFlowGenerator,
            DataFlowParseService dataFlowParser)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

            // Repo này đã được phân tích trước đó và đã có sẵn trong database -> dùng lại, không cần
            // clone/kéo source code về backend và parse lại từ đầu nữa.
            var cachedRuns = await _unitOfWork.AnalysisRuns
                .FindAsync(r => r.RepositoryPath.ToLower() == trimmedRepoPath.ToLower());
            var cachedRun = cachedRuns.OrderByDescending(r => r.CreatedAt).FirstOrDefault();

            if (cachedRun != null)
            {
                var cachedEdgesCount = (await _unitOfWork.CallGraphEdges
                    .FindAsync(e => e.AnalysisRunId == cachedRun.Id)).Count();
                var cachedMethodsCount = (await _unitOfWork.MethodSources
                    .FindAsync(m => m.AnalysisRunId == cachedRun.Id)).Count();

                return new AnalysisResponseDto
                {
                    Message = "Repository đã được phân tích trước đó — dùng lại dữ liệu có sẵn trong database.",
                    AnalysisRunId = cachedRun.Id,
                    EdgesCount = cachedEdgesCount,
                    MethodsCount = cachedMethodsCount
                };
            }

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
                        ConditionContext = edge.ConditionContext,
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
                    }).ToList(),
                    GlobalNodeCount = result.MethodSources.Count
                };

                await _unitOfWork.AnalysisRuns.AddAsync(analysisRun);
                var allIntraEdges = new List<DataFlowEdge>();
                foreach (var source in analysisRun.MethodSources)
                {
                    var methodDataFlows = _dataFlowParser.ParseIntraMethodDataFlow(analysisRun.Id, source.ClassName, source.MethodName, source.SourceCode);
                    allIntraEdges.AddRange(methodDataFlows);
                }

                var features = _businessFlowParser.ParseBusinessFlows(analysisRun.Id, analysisRun.CallGraphEdges);
                if (features.Any())
                {
                    var methodSourcesList = analysisRun.MethodSources.ToList();
                    foreach (var flow in features)
                    {
                        flow.DataFlowMermaidGraph = _businessCallDataFlowGenerator.GenerateCallDataFlow(flow, methodSourcesList, allIntraEdges);
                    }
                    await _unitOfWork.Features.AddRangeAsync(features);
                }

                await _unitOfWork.SaveChangesAsync();

                string businessJsonPath = Path.Combine(targetPath, "template_business.json");

                if (!File.Exists(businessJsonPath))
                {
                    throw new NotFoundException($"File template_business.json không tồn tại trong thư mục: {targetPath}");
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





