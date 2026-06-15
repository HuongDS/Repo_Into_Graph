using Repo_Into_Graph.Models;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Analysis;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Feature;
using Repo_Into_Graph.Repo_Into_Graph.Models.Method;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;
using Repo_Into_Graph.Repo_Into_Graph.Services.GitService;
using Repo_Into_Graph.Repo_Into_Graph.Services.Mapper;
using Repo_Into_Graph.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.Analysis
{
    public class AnalysisService : IAnalysisService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly GraphMapperService _graphMapper;
        private readonly IGitService _gitService;

        public AnalysisService(IUnitOfWork unitOfWork, GraphMapperService graphMapper, IGitService gitService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _graphMapper = graphMapper ?? throw new ArgumentNullException(nameof(graphMapper));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        }

        public async Task<AnalysisResponseDto> AnalyzeRepositoryAsync(string repositoryPath, string? outputDir)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                throw new ArgumentException("Đường dẫn repository hoặc URL git không được để trống.");
            }

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
                throw new DirectoryNotFoundException($"Thư mục local không tồn tại: {trimmedRepoPath}");
            }

            try
            {
                Directory.CreateDirectory(targetOutputDir);
                var analyzer = new CodeAnalyzer(targetPath);
                var result = await analyzer.AnalyzeAsync();

                // Xóa các lượt phân tích cũ cho repository này
                var existingRuns = await _unitOfWork.AnalysisRuns
                    .FindAsync(r => r.RepositoryPath.ToLower() == trimmedRepoPath.ToLower());

                if (existingRuns.Any())
                {
                    _unitOfWork.AnalysisRuns.DeleteRange(existingRuns);
                    await _unitOfWork.SaveChangesAsync();
                }

                // Tạo AnalysisRun mới
                var analysisRun = new Models.Analysis.AnalysisRun
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
                        CreatedAt = DateTime.UtcNow
                    }).ToList()
                };

                await _unitOfWork.AnalysisRuns.AddAsync(analysisRun);
                await _unitOfWork.SaveChangesAsync();

                // Thực hiện ánh xạ đồ thị
                string featuresJsonPath = "./template_feature.json";
                if (File.Exists(featuresJsonPath))
                {
                    await _graphMapper.ProcessAndMapGraphAsync(analysisRun.Id, featuresJsonPath);
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
