using System.Linq;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment.DifficultyEvaluate
{
    /// <summary>
    /// Triển khai tính toán các chỉ số Độ Khó theo lý thuyết đồ thị (V(G), L_q, Path Type).
    /// </summary>
    public class DifficultyAssessmentService : IDifficultyAssessmentService
    {
        private readonly ILogger<DifficultyAssessmentService> _logger;

        public DifficultyAssessmentService(ILogger<DifficultyAssessmentService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<DifficultyAssessmentResultDto> AssessAsync(DifficultyAssessmentRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            var activeNodes = request.ActiveNodes ?? new List<GraphNodeDto>();

            _logger.LogInformation(
                "[DifficultyAssessment] Bắt đầu – {N} Active Nodes | E_q={E}",
                activeNodes.Count, request.TotalEdgesInSubgraph);

            // Chỉ số 1: Cyclomatic Complexity – V(G) = E_q - V_q + 2P  (McCabe 1976)
            //
            // V_q phải đếm số nút PHÂN BIỆT: ExtractedPath có thể lặp lại cùng một nút,
            // nếu đếm cả bản lặp thì V(G) sẽ bị âm một cách vô nghĩa.
            int vq = activeNodes.Select(n => n.NodeId).Distinct().Count();
            int eq = Math.Max(0, request.TotalEdgesInSubgraph);   // cạnh THẬT của đồ thị con cảm sinh
            int pq = Math.Max(1, request.ConnectedComponents);    // số thành phần liên thông

            // Đồ thị rỗng thì không có gì để đo.
            int cyclomaticComplexity = vq == 0 ? 0 : eq - vq + 2 * pq;

            // Với đồ thị con cảm sinh hợp lệ luôn có E_q >= V_q - P, nên V(G) >= P >= 1.
            // Nếu rơi xuống dưới 1 nghĩa là dữ liệu đầu vào mâu thuẫn (E_q hoặc P sai) -> báo động.
            if (vq > 0 && cyclomaticComplexity < 1)
            {
                _logger.LogError(
                    "[DifficultyAssessment] DU LIEU MAU THUAN: V(G)={VG} < 1 voi V_q={V}, E_q={E}, P={P}. " +
                    "Kiem tra lai cach dem canh/thanh phan lien thong o tang Controller.",
                    cyclomaticComplexity, vq, eq, pq);
            }

            // Chỉ số 2: Impact Path Length – L_q = V_q - 1
            int impactPathLength = Math.Max(0, activeNodes.Count - 1);

            // Chỉ số 3: Đếm DecisionGateway trong chuỗi Active Nodes
            int gatewaysCount = activeNodes.Count(n =>
                string.Equals(n.NodeType, "DecisionGateway", StringComparison.OrdinalIgnoreCase));

            var gatewayNames = activeNodes
                .Where(n => string.Equals(n.NodeType, "DecisionGateway", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.NodeName)
                .ToList();

            // Phân loại Path Type
            string pathType = ClassifyPathType(gatewaysCount);

            // Phân loại Độ Khó (nhất quán với WorkflowAssessmentService.cs)
            string level = ClassifyDifficulty(gatewaysCount, impactPathLength);

            // Xây dựng Reasoning
            string reasoning = BuildReasoning(
                level, cyclomaticComplexity, impactPathLength,
                gatewaysCount, gatewayNames, vq, eq, pq, activeNodes.Count, pathType);

            _logger.LogInformation(
                "[DifficultyAssessment] Hoàn thành – Level={Level} | V(G)={VG} (V_q={V}, E_q={E}, P={P}) | L_q={L} | Gateways={G}",
                level, cyclomaticComplexity, vq, eq, pq, impactPathLength, gatewaysCount);

            var result = new DifficultyAssessmentResultDto
            {
                Level                = level,
                CyclomaticComplexity = cyclomaticComplexity,
                ImpactPathLength     = impactPathLength,
                GatewaysCount        = gatewaysCount,
                PathType             = pathType,
                Reasoning            = reasoning
            };

            return Task.FromResult(result);
        }

        // PRIVATE: Phân loại helpers

        /// <summary>
        /// Phân loại loại nhánh kích hoạt dựa trên số DecisionGateway.
        /// </summary>
        private static string ClassifyPathType(int gatewaysCount) => gatewaysCount switch
        {
            0 => "Happy Path",
            1 => "Single Exception",
            _ => "Double Exception"
        };

        /// <summary>
        /// Phân loại Độ Khó — nhất quán với logic <c>ClassifyDifficulty</c>
        /// trong <see cref="WorkflowAssessmentService"/> (dòng 543–548).
        /// </summary>
        private static string ClassifyDifficulty(int gateways, int pathLength)
        {
            if (gateways >= 2 || pathLength >= 3) return "Khó";
            if (gateways == 1)                    return "Trung bình";
            return "Dễ";
        }

        /// <summary>
        /// Xây dựng chuỗi lời chứng minh định lượng đầy đủ cho người đọc.
        /// </summary>
        private static string BuildReasoning(
            string   level,
            int      cyclomaticComplexity,
            int      impactPathLength,
            int      gatewaysCount,
            List<string> gatewayNames,
            int      vq,
            int      eq,
            int      pq,
            int      stepCount,
            string   pathType)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append($"Câu hỏi đạt mức {level.ToUpper()} (Path Type: {pathType}). ");
            sb.Append($"Luồng nghiệp vụ trải qua {stepCount} bước xử lý, ");
            sb.Append($"tạo thành {impactPathLength} bước chuyển tiếp logic (L_q = {stepCount} - 1 = {impactPathLength}). ");

            // Độ phức tạp tuần hoàn
            sb.Append($"Độ phức tạp tuần hoàn V(G) = E_q - V_q + 2P = {eq} - {vq} + 2*{pq} = {cyclomaticComplexity}, ");
            sb.Append($"tương ứng với {cyclomaticComplexity} kịch bản kiểm thử (test cases) cần thiết. ");

            // Gateways
            if (gatewaysCount == 0)
            {
                sb.Append("Luồng đi thẳng tuyến tính (Happy Path), không có rẽ nhánh điều kiện.");
            }
            else if (gatewaysCount == 1)
            {
                sb.Append($"Luồng kích hoạt đúng 1 điều kiện rẽ nhánh ngoại lệ ");
                if (gatewayNames.Count > 0)
                    sb.Append($"('{gatewayNames[0]}') ");
                sb.Append("— đòi hỏi người trả lời nắm được tình huống kiểm tra nghiệp vụ cơ bản.");
            }
            else
            {
                sb.Append($"Luồng kích hoạt đồng thời {gatewaysCount} điều kiện rẽ nhánh ngoại lệ");
                if (gatewayNames.Count > 0)
                {
                    sb.Append(": ");
                    sb.Append(string.Join(", ", gatewayNames.Select((n, i) => $"({i + 1}) '{n}'")));
                }
                sb.Append(" — đòi hỏi người trả lời nắm vững thứ tự ưu tiên và xử lý xung đột logic phức tạp.");
            }

            return sb.ToString();
        }
    }
}
