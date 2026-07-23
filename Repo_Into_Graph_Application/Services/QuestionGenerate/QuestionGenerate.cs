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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIService _aIService;

        public QuestionGenerate(IUnitOfWork unitOfWork,
            IAIService aIService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _aIService = aIService;
        }

        public async Task<GenerateQuestionsResponse> GenerateQuestionsAsync(GenerateQuestionsRequest request)
        {
            if (request == null)
                throw new BadRequestException("Yêu cầu không được để trống.");

            // 1. Load Business
            var businessModel = await _unitOfWork.Businesses.GetByIdAsync(request.BusinessId);

            if (businessModel == null)
                throw new NotFoundException("Business", request.BusinessId);

            // 2. Load các Feature (Luồng nghiệp vụ) được map với Business này
            var featureBusinessMappings = await _unitOfWork.FeatureBusinessMappings.GetFeatureIdsByBusinessIdAsync(request.BusinessId);

            var features = await _unitOfWork.Features.GetFeaturesWithStepsByIdsAsync(featureBusinessMappings);

            // 3. Load Source Code (MethodSource) từ các Feature đó
            var featureMethodMappings = await _unitOfWork.FeatureMethodMappings.GetMappingsWithMethodSourceByFeatureIdsAsync(featureBusinessMappings);

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
                fewShotExamples = await _unitOfWork.FewShotExamples.GetByIdsAsync(request.FewShotExampleIds);
            }
            else if (!string.IsNullOrWhiteSpace(request.Difficulty))
            {
                fewShotExamples = await _unitOfWork.FewShotExamples.GetByDifficultyAsync(request.Difficulty, 5);
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

            return new GenerateQuestionsResponse
            {
                BusinessId = businessModel.Id,
                BusinessName = businessModel.BusinessName,
                EntryPoint = string.Join(", ", features.Select(f => f.EntryPoint)),
                TotalSteps = features.Sum(f => f.Steps?.Count ?? 0),
                FewShotUsed = fewShotExamples?.Count() ?? 0,
                GeneratedQuestionDtos = questions
            };
        }
    }
}





