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

        public async Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestionsAsync(GenerateQuestionsRequest request)
        {
            if (request == null)
                throw new BadRequestException("Yêu cầu không được để trống.");

            var feature = await _context.FeatureRecords.FindAsync(request.FeatureId);
            if (feature == null)
                throw new NotFoundException("Feature", request.FeatureId);

            var featureMappings = await _context.FeatureMethodMappings
                .Include(fmm => fmm.MethodSource)
                .Where(fmm => fmm.FeatureId == request.FeatureId)
                .ToListAsync();

            var methodSources = featureMappings
                .Where(fmm => fmm.MethodSource != null)
                .Select(fmm => fmm.MethodSource!)
                .ToList();

            if (!methodSources.Any())
                throw new UnprocessableException("Feature này không có đoạn code logic nào được ánh xạ trong hệ thống.");

            var codeBuilder = new StringBuilder();
            foreach (var method in methodSources)
            {
                codeBuilder.AppendLine($"// Class: {method.ClassName}, Method: {method.MethodName}");
                codeBuilder.AppendLine(method.SourceCode);
                codeBuilder.AppendLine();
            }

            var classNames = methodSources.Select(m => m.ClassName.Trim().ToLower()).Distinct().ToList();

            var callGraphEdges = await _context.CallGraphEdges
                .Where(e => e.AnalysisRunId == feature.AnalysisRunId &&
                            (classNames.Contains(e.CallerClass.Trim().ToLower()) ||
                             classNames.Contains(e.CalleeClass.Trim().ToLower())))
                .ToListAsync();

            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Call Graph Relationships:");
            if (callGraphEdges.Any())
                foreach (var edge in callGraphEdges)
                    contextBuilder.AppendLine($"- {edge.CallerClass}.{edge.CallerMethod} calls {edge.CalleeClass}.{edge.CalleeMethod}");
            else
                contextBuilder.AppendLine("No call graph relationships found in database.");

            int numberOfQuestions = request.NumberOfQuestions;
            if (numberOfQuestions <= 0 || numberOfQuestions > 20)
                numberOfQuestions = 5;

            return await _aIService.GenerateQuestions(numberOfQuestions, request, codeBuilder.ToString(), contextBuilder.ToString());
        }

        // ─── Generate from Business Flow ─────────────────────────────────────────

        public async Task<GenerateQuestionsFromFlowResponse> GenerateQuestionsFromFlowAsync(
            GenerateQuestionsFromFlowRequest request)
        {
            // Load BusinessFlow cùng Steps từ DB
            var businessFlow = await _context.BusinessFlows
                .Include(f => f.Steps)
                .FirstOrDefaultAsync(f => f.Id == request.BusinessFlowId);

            if (businessFlow == null)
                throw new NotFoundException("Business Flow", request.BusinessFlowId);

            // Load few-shot examples: ưu tiên theo danh sách ID, nếu không có thì lọc theo difficulty
            IEnumerable<FewShotExample>? fewShotExamples = null;

            if (request.FewShotExampleIds != null && request.FewShotExampleIds.Count > 0)
            {
                var ids = request.FewShotExampleIds;
                fewShotExamples = await _context.FewShotExamples
                    .Where(e => ids.Contains(e.Id))
                    .ToListAsync();
            }
            else if (!string.IsNullOrWhiteSpace(request.Difficulty))
            {
                fewShotExamples = await _context.FewShotExamples
                    .Where(e => e.Difficulty.ToLower() == request.Difficulty.ToLower())
                    .Take(5)
                    .ToListAsync();
            }

            var questions = await _aIService.GenerateQuestionsFromBusinessFlowAsync(
                businessFlow      : businessFlow,
                numberOfQuestions : request.NumberOfQuestions,
                difficulty        : request.Difficulty,
                additionalContext : request.AdditionalContext,
                fewShotExamples   : (IEnumerable<FewShotExample>?)fewShotExamples);
            var evaluationResults = await _aIService.EvaluateQuestionsAsync(
                dataFlowMermaidGraph: businessFlow.DataFlowMermaidGraph,
                codeBuilder: null,
                generatedQuestions: questions);

            return new GenerateQuestionsFromFlowResponse
            {
                BusinessFlowId   = businessFlow.Id,
                BusinessFlowName = businessFlow.Name,
                EntryPoint       = businessFlow.EntryPoint,
                TotalSteps       = businessFlow.Steps?.Count ?? 0,
                FewShotUsed      = fewShotExamples?.Count() ?? 0,
                Questions        = questions
            };
        }
    }
}





