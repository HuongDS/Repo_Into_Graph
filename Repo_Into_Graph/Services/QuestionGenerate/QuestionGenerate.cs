using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate;
using Repo_Into_Graph.Models;
using Repo_Into_Graph.Repo_Into_Graph.Services.AI;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.QuestionGenerate
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

        public async Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestionsAsync(GenerateQuestionsRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), "Yêu cầu không được để trống.");
            }

            // 1. Get the Feature record
            var feature = await _context.FeatureRecords.FindAsync(request.FeatureId);
            if (feature == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy Feature với ID: {request.FeatureId}");
            }

            // 2. Get all method sources mapped to this feature
            var featureMappings = await _context.FeatureMethodMappings
                .Include(fmm => fmm.MethodSource)
                .Where(fmm => fmm.FeatureId == request.FeatureId)
                .ToListAsync();

            var methodSources = featureMappings
                .Where(fmm => fmm.MethodSource != null)
                .Select(fmm => fmm.MethodSource!)
                .ToList();

            if (!methodSources.Any())
            {
                throw new InvalidOperationException("Feature này không có đoạn code logic nào được ánh xạ trong hệ thống.");
            }

            // Combine all source codes of methods in this feature
            var codeBuilder = new StringBuilder();
            foreach (var method in methodSources)
            {
                codeBuilder.AppendLine($"// Class: {method.ClassName}, Method: {method.MethodName}");
                codeBuilder.AppendLine(method.SourceCode);
                codeBuilder.AppendLine();
            }

            // 3. Get all call graph edges related to this feature's classes
            var classNames = methodSources.Select(m => m.ClassName.Trim().ToLower()).Distinct().ToList();

            //var callGraphEdges = await _context.CallGraphEdges
            //    .Where(e => e.AnalysisRunId == feature.AnalysisRunId &&
            //                (classNames.Contains(e.CallerClass.Trim().ToLower()) ||
            //                 classNames.Contains(e.CalleeClass.Trim().ToLower())))
            //    .ToListAsync();

            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Call Graph Relationships:");
            //if (callGraphEdges.Any())
            //{
            //    foreach (var edge in callGraphEdges)
            //    {
            //        contextBuilder.AppendLine($"- {edge.CallerClass}.{edge.CallerMethod} calls {edge.CalleeClass}.{edge.CalleeMethod}");
            //    }
            //}
            //else
            //{
            //    contextBuilder.AppendLine("No call graph relationships found in database.");
            //}
            //if (callGraphEdges.Any())
            //{
            //    foreach (var edge in callGraphEdges)
            //    {
            //        contextBuilder.AppendLine($"- {edge.CallerClass}.{edge.CallerMethod} calls {edge.CalleeClass}.{edge.CalleeMethod}");
            //    }
            //}
            //else
            //{
            //    contextBuilder.AppendLine("No call graph relationships found in database.");
            //}

            // Limit number of questions
            int numberOfQuestions = request.NumberOfQuestions;
            if (numberOfQuestions <= 0 || numberOfQuestions > 20)
            {
                numberOfQuestions = 5;
            }

            return await _aIService.GenerateQuestions(numberOfQuestions, request, codeBuilder.ToString(), contextBuilder.ToString());
        }
    }
}
