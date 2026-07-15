using Repo_Into_Graph_DataAccess.Database;
using Repo_Into_Graph_DataAccess.Repository.Interface;
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
using Repo_Into_Graph_Application.Services.Caculation;

namespace Repo_Into_Graph_Application.Services.QuestionGenerate
{
    public class QuestionGenerate : IQuestionGenerate
    {
        private readonly IBusinessRepository _businessRepository;
        private readonly IFeatureBusinessMappingRepository _featureBusinessMappingRepository;
        private readonly IFeatureRepository _featureRepository;
        private readonly IFeatureMethodMappingRepository _featureMethodMappingRepository;
        private readonly IFewShotExampleRepository _fewShotExampleRepository;
        private readonly IAIService _aIService;
        private readonly ICaculationService _caculationService;

        public QuestionGenerate(
            IBusinessRepository businessRepository,
            IFeatureBusinessMappingRepository featureBusinessMappingRepository,
            IFeatureRepository featureRepository,
            IFeatureMethodMappingRepository featureMethodMappingRepository,
            IFewShotExampleRepository fewShotExampleRepository,
            IAIService aIService, 
            ICaculationService caculationService)
        {
            _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
            _featureBusinessMappingRepository = featureBusinessMappingRepository ?? throw new ArgumentNullException(nameof(featureBusinessMappingRepository));
            _featureRepository = featureRepository ?? throw new ArgumentNullException(nameof(featureRepository));
            _featureMethodMappingRepository = featureMethodMappingRepository ?? throw new ArgumentNullException(nameof(featureMethodMappingRepository));
            _fewShotExampleRepository = fewShotExampleRepository ?? throw new ArgumentNullException(nameof(fewShotExampleRepository));
            _aIService = aIService;
            _caculationService = caculationService;
        }



        public async Task<GenerateQuestionsResponse> GenerateQuestionsAsync(GenerateQuestionsRequest request)
        {
            if (request == null)
                throw new BadRequestException("Yêu cầu không được để trống.");

            // 1. Load Business
            var businessModel = await _businessRepository.GetByIdAsync(request.BusinessId);

            if (businessModel == null)
                throw new NotFoundException("Business", request.BusinessId);

            // 2. Load các Feature (Luồng nghiệp vụ) được map với Business này
            var featureBusinessMappings = await _featureBusinessMappingRepository.GetFeatureIdsByBusinessIdAsync(request.BusinessId);

            var features = await _featureRepository.GetFeaturesWithStepsByIdsAsync(featureBusinessMappings);

            // 3. Load Source Code (MethodSource) từ các Feature đó
            var featureMethodMappings = await _featureMethodMappingRepository.GetMappingsWithMethodSourceByFeatureIdsAsync(featureBusinessMappings);

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
                fewShotExamples = await _fewShotExampleRepository.GetByIdsAsync(request.FewShotExampleIds);
            }
            else if (!string.IsNullOrWhiteSpace(request.Difficulty))
            {
                fewShotExamples = await _fewShotExampleRepository.GetByDifficultyAsync(request.Difficulty, 5);
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

            var codeCoverage = await _caculationService.CalculateCodeCoverage(questions, request.BusinessId);

            return new GenerateQuestionsResponse
            {
                BusinessId = businessModel.Id,
                BusinessName = businessModel.BusinessName,
                EntryPoint = string.Join(", ", features.Select(f => f.EntryPoint)),
                TotalSteps = features.Sum(f => f.Steps?.Count ?? 0),
                FewShotUsed = fewShotExamples?.Count() ?? 0,
                GeneratedQuestionDtos = questions,
                CodeCoverage = codeCoverage,
            };
        }
    }
}





