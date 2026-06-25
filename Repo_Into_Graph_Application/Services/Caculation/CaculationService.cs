using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Caculation
{
    public class CaculationService : ICaculationService
    {
        private readonly IBusinessRepository _businessRepository;
        public CaculationService(IBusinessRepository businessRepository)
        {
            _businessRepository = businessRepository;
        }
        public async Task<double> CalculateCodeCoverage(IEnumerable<GeneratedQuestionDto> generatedQuestions, Guid businessID)
        {
            var callerMethods = await _businessRepository.GetAllMethod(businessID);
            if (callerMethods == null || !callerMethods.Any())
            {
                return 0.0;
            }
            var totalMethodsSet = callerMethods
                                    .Where(m => !string.IsNullOrWhiteSpace(m))
                                    .Select(m => m.Trim().Split('.').Last().ToLower()) 
                                    .ToHashSet();
            var targetedMethodsSet = new HashSet<string>();

            if (generatedQuestions != null)
            {
                foreach (var q in generatedQuestions)
                {
                    if (q.TargetedEntryPoints == null) continue;

                    foreach (var method in q.TargetedEntryPoints)
                    {
                        if (string.IsNullOrWhiteSpace(method)) continue;

                        var cleanAiMethodName = method.Trim().
                                                Replace(".", "_").ToLower();

                        if (totalMethodsSet.Contains(cleanAiMethodName))
                        {
                            targetedMethodsSet.Add(cleanAiMethodName);
                        }
                    }
                }
            }

            double coverage = (targetedMethodsSet.Count*1.0) / totalMethodsSet.Count;

            return coverage;
        }

       
    }
}
