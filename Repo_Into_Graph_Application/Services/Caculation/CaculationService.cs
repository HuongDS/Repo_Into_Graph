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
            // GetAllMethod trả về dạng "CalleeClass_CalleeMethod" => chuẩn hoá về chữ thường
            var totalMethodsSet = callerMethods
                                    .Where(m => !string.IsNullOrWhiteSpace(m))
                                    .Select(m => m.Trim().ToLower())
                                    .ToHashSet();

            if (totalMethodsSet.Count == 0)
            {
                return 0.0;
            }

            // Tập method được cả bộ câu hỏi chạm tới (dùng để tính coverage tổng)
            var overallTargetedSet = new HashSet<string>();

            if (generatedQuestions != null)
            {
                foreach (var q in generatedQuestions)
                {
                    // Tập method mà RIÊNG câu hỏi này chạm tới
                    var questionTargetedSet = new HashSet<string>();

                    if (q.TargetedEntryPoints != null)
                    {
                        foreach (var method in q.TargetedEntryPoints)
                        {
                            if (string.IsNullOrWhiteSpace(method)) continue;

                            // AI trả về dạng "Class.Method" => đổi "." thành "_" cho khớp với totalMethodsSet
                            var cleanAiMethodName = method.Trim().Replace(".", "_").ToLower();

                            if (totalMethodsSet.Contains(cleanAiMethodName))
                            {
                                questionTargetedSet.Add(cleanAiMethodName);
                                overallTargetedSet.Add(cleanAiMethodName);
                            }
                        }
                    }

                    // Gán độ bao phủ riêng cho từng câu hỏi
                    q.Coverage = (questionTargetedSet.Count * 1.0) / totalMethodsSet.Count;
                }
            }

            double coverage = (overallTargetedSet.Count * 1.0) / totalMethodsSet.Count;

            return coverage;
        }

       
    }
}
