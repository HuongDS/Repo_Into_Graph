using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Exceptions;
using Repo_Into_Graph_Application.Services.AI;
using Repo_Into_Graph_Application.Services.Analysis;
using Repo_Into_Graph_Application.Services.Caculation;
using Repo_Into_Graph_Application.Services.GitService;
using Repo_Into_Graph_DataAccess.Database;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.FewShot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.QuestionGenerate
{
    public class QuestionGenerate : IQuestionGenerate
    {
        private readonly AnalysisDbContext _context;
        private readonly IAIService _aIService;
        private readonly ICaculationService _caculationService;
        private readonly IGitService _gitService;


        public QuestionGenerate(AnalysisDbContext context, IAIService aIService, ICaculationService caculationService, IGitService gitService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _aIService = aIService;
            _caculationService = caculationService;
            _gitService = gitService;
        }

        

        public async Task<GenerateQuestionsResponse> GenerateQuestionsAsync(GenerateQuestionsRequest request)
        {
            if (request == null)
                throw new BadRequestException("Yêu cầu không được để trống.");

            // 1. Load Business
            var businessModel = await _context.Businesses
                .FirstOrDefaultAsync(b => b.Id == request.BusinessId);

            if (businessModel == null)
                throw new NotFoundException("Business", request.BusinessId);

            // 2. Load các Feature (Luồng nghiệp vụ) được map với Business này
            var featureBusinessMappings = await _context.FeatureBusinessMappings
                .Where(m => m.BusinessId == request.BusinessId)
                .Select(m => m.FeatureId)
                .ToListAsync();

            var features = await _context.Features
                .Include(f => f.Steps)
                .Where(f => featureBusinessMappings.Contains(f.Id))
                .ToListAsync();

            // 3. Load Source Code (MethodSource) từ các Feature đó
            var featureMethodMappings = await _context.FeatureMethodMappings
                .Include(m => m.MethodSource)
                .Where(m => featureBusinessMappings.Contains(m.FeatureId))
                .ToListAsync();

            var methodSources = featureMethodMappings
                .Where(m => m.MethodSource != null)
                .Select(m => m.MethodSource!)
                .DistinctBy(m => m.Id)
                .ToList();

            var codeBuilder = new StringBuilder();
            if (methodSources.Any())
            {
                foreach (var method in methodSources)
                {
                    codeBuilder.AppendLine($"// Class: {method.ClassName}, Method: {method.MethodName}");
                    codeBuilder.AppendLine(method.SourceCode);
                    codeBuilder.AppendLine();
                }
            }
            else
            {
                codeBuilder.AppendLine("// Không tìm thấy Source Code nào được map cho Business này.");
            }

            var contextBuilder = new StringBuilder();
            if (features.Any())
            {
                foreach (var feature in features)
                {
                    contextBuilder.AppendLine($"### Tên luồng: {feature.Name}");
                    //contextBuilder.AppendLine($"Entry Point: {feature.EntryPoint}");

                    //contextBuilder.AppendLine("Chuỗi bước gọi (Call chain):");
                    //if (feature.Steps != null && feature.Steps.Count > 0)
                    //{
                    //    foreach (var step in feature.Steps.OrderBy(s => s.StepOrder))
                    //    {
                    //        contextBuilder.AppendLine($"  [{step.StepOrder}] {step.CallerClass}.{step.CallerMethod} --> {step.CalleeClass}.{step.CalleeMethod}");
                    //    }
                    //}
                    //else
                    //{
                    //    contextBuilder.AppendLine("  (Không có dữ liệu bước gọi)");
                    //}

                    if (!string.IsNullOrWhiteSpace(feature.DataFlowMermaidGraph))
                    {
                        contextBuilder.AppendLine("Data Mermaid Diagram:");
                        contextBuilder.AppendLine(feature.DataFlowMermaidGraph);
                    }
                    contextBuilder.AppendLine();
                }
            }
            else
            {
                contextBuilder.AppendLine("Không có luồng nghiệp vụ (Feature) nào được map với Business này.");
            }

            // 4. Load few-shot examples
            IEnumerable<FewShotExample>? fewShotExamples = null;
            if (request.FewShotExampleIds != null && request.FewShotExampleIds.Count > 0)
            {
                fewShotExamples = await _context.FewShotExamples
                    .Where(e => request.FewShotExampleIds.Contains(e.Id))
                    .ToListAsync();
            }
            else if (!string.IsNullOrWhiteSpace(request.Difficulty))
            {
                fewShotExamples = await _context.FewShotExamples
                    .Where(e => e.Difficulty.ToLower() == request.Difficulty.ToLower())
                    .Take(5)
                    .ToListAsync();
            }

            int numberOfQuestions = request.NumberOfQuestions;
            if (numberOfQuestions <= 0 || numberOfQuestions > 20)
                numberOfQuestions = 5;

            // 5. Generate Questions
            var questions = await _aIService.GenerateUnifiedQuestionsAsync(
                businessName: businessModel.BusinessName,
                codeBuilder: codeBuilder.ToString(),
                contextBuilder: contextBuilder.ToString(),
                numberOfQuestions: numberOfQuestions,
                difficulty: request.Difficulty,
                additionalContext: request.Description,
         
                fewShotExamples: fewShotExamples);

            var codeCoverage = await _caculationService.CalculateCodeCoverage(questions,request.BusinessId);

            return new GenerateQuestionsResponse
            {
                BusinessId = businessModel.Id,
                BusinessName = businessModel.BusinessName,
                EntryPoint = string.Join(", ", features.Select(f => f.EntryPoint)),
                TotalSteps = features.Sum(f => f.Steps?.Count ?? 0),
                FewShotUsed = fewShotExamples?.Count() ?? 0,
                GeneratedQuestionDtos= questions,
                CodeCoverage = codeCoverage,

            };
        }

        public async Task<GenerateQuestionsResponse> GenerateQuestionsFullAsync(GenerateQuestionFullRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Repo_path))
                throw new BadRequestException("Đường dẫn repository hoặc URL git không được để trống.");

            string trimmedRepoPath = request.Repo_path.Trim('"', ' ');
            bool isGitUrl = _gitService.IsGitUrl(trimmedRepoPath);
            string targetPath = trimmedRepoPath;
            if (isGitUrl)
            {
                targetPath = await _gitService.CloneRepositoryAsync(trimmedRepoPath);
               // isTempDirectory = true;
            }
            var analyzer = new CodeAnalyzer(targetPath);
            var result = await analyzer.AnalyzeAsync();

            var questions = await _aIService.GenerateUnifiedQuestionsAsync(
               businessName: request.BusinessName,
               codeBuilder: result.MethodSources.ToString(),
               contextBuilder: "",
               numberOfQuestions:request.NumberOfQuestions,
               difficulty: request.Difficulty,
               additionalContext: "",

               fewShotExamples: new List<FewShotExample>() );


            return new GenerateQuestionsResponse
            {
                
                BusinessName = request.BusinessName,  
                GeneratedQuestionDtos = questions,
         
            };




        }
    }
}





