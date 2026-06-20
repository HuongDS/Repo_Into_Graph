using Repo_Into_Graph_DataAccess.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Exceptions;
using Repo_Into_Graph_DataAccess.Models.FewShot;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_Application.Services.AI;
using Repo_Into_Graph_Application.Dtos.QuestionEvalution;

namespace Repo_Into_Graph_Application.Services.QuestionGenerate
{
    public class QuestionGenerate : IQuestionGenerate
    {
        private readonly AnalysisDbContext _context;
        private readonly IAIService _aIService;

        public QuestionGenerate(AnalysisDbContext context, IAIService aIService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _aIService = aIService;
        }

        public async Task<IEnumerable<QuestionEvaluationResultDto>> EvaluateQuestionsAsync(string dataFlowMermaidGraph, string codeBuilder, IEnumerable<GeneratedQuestionDto> generatedQuestions)
        {
            return await _aIService.EvaluateQuestionsAsync(dataFlowMermaidGraph, codeBuilder, generatedQuestions);
        }

        public async Task<GenerateQuestionsResponse> GenerateQuestionsAsync(GenerateQuestionsRequest request)
        {
            if (request == null)
                throw new BadRequestException("Yêu cầu không được để trống.");

            // 1. Load Feature (Context luồng, Steps, Mermaid)
            var featureModel = await _context.Features
                .Include(f => f.Steps)
                .FirstOrDefaultAsync(f => f.Id == request.FeatureId);

            if (featureModel == null)
                throw new NotFoundException("Feature", request.FeatureId);

            // 2. Load các Business được map với Feature này
            var featureBusinessMappings = await _context.FeatureBusinessMappings
                .Where(m => m.FeatureId == request.FeatureId)
                .Select(m => m.BusinessId)
                .ToListAsync();

            // 3. Load Source Code (MethodSource) từ các Business đó
            var businessMethodMappings = await _context.BusinessMethodMappings
                .Include(m => m.MethodSource)
                .Where(m => featureBusinessMappings.Contains(m.BusinessId))
                .ToListAsync();

            var methodSources = businessMethodMappings
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
                codeBuilder.AppendLine("// Không tìm thấy Source Code nào được map cho Feature này.");
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
                feature: featureModel,
                codeBuilder: codeBuilder.ToString(),
                numberOfQuestions: numberOfQuestions,
                difficulty: request.Difficulty,
                additionalContext: request.Description,
                fewShotExamples: fewShotExamples);

            // 6. Evaluate Questions
            var evaluationResults = await _aIService.EvaluateQuestionsAsync(
                dataFlowMermaidGraph: featureModel.DataFlowMermaidGraph ?? string.Empty,
                codeBuilder: codeBuilder.ToString(),
                generatedQuestions: questions);

            return new GenerateQuestionsResponse
            {
                FeatureId   = featureModel.Id,
                FeatureName = featureModel.Name,
                EntryPoint  = featureModel.EntryPoint,
                TotalSteps  = featureModel.Steps?.Count ?? 0,
                FewShotUsed = fewShotExamples?.Count() ?? 0,
                EvaluatedQuestions = evaluationResults
            };
        }
    }
}





