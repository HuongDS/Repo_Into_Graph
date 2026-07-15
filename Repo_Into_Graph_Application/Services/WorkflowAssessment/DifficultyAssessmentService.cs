using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    /// <summary>
    /// Triển khai <see cref="IDifficultyAssessmentService"/>.
    ///
    /// <para>
    /// Thực thi thuần toán học — không cần Gemini API, không cần Database.
    /// Ba chỉ số được tính theo lý thuyết đồ thị:
    /// </para>
    ///
    /// <list type="number">
    ///   <item>
    ///     <term>Cyclomatic Complexity V(G)</term>
    ///     <description>V(G) = E_q - V_q + 2 (công thức McCabe)</description>
    ///   </item>
    ///   <item>
    ///     <term>Impact Path Length L_q</term>
    ///     <description>L_q = |ActiveNodes| - 1 (số bước dịch chuyển)</description>
    ///   </item>
    ///   <item>
    ///     <term>Path Type Classification</term>
    ///     <description>
    ///       Happy Path (0 gateway) |
    ///       Single Exception (1 gateway) |
    ///       Double Exception (≥ 2 gateways)
    ///     </description>
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// Phân loại Độ Khó cuối cùng dựa theo logic <c>ClassifyDifficulty</c>
    /// nhất quán với <see cref="WorkflowAssessmentService"/>:
    /// <code>
    ///   gateways ≥ 2 || pathLength ≥ 3  → "Khó"
    ///   gateways == 1                   → "Trung bình"
    ///   default                         → "Dễ"
    /// </code>
    /// </para>
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

            // ──────────────────────────────────────────────────────────────────
            // Chỉ số 1: Cyclomatic Complexity – V(G) = E_q - V_q + 2
            // ──────────────────────────────────────────────────────────────────
            int vq = activeNodes.Count;      // Số nút của đồ thị con G_q
            int eq = request.TotalEdgesInSubgraph;
            // Đảm bảo E_q hợp lệ: phải >= V_q - 1 (cây tối giản)
            if (eq < Math.Max(0, vq - 1))
            {
                _logger.LogWarning(
                    "[DifficultyAssessment] TotalEdgesInSubgraph ({E}) nhỏ hơn V_q-1 ({V}). " +
                    "Fallback: tính E_q = V_q - 1 (đồ thị tuyến tính).",
                    eq, vq - 1);
                eq = Math.Max(0, vq - 1);
            }

            int cyclomaticComplexity = eq - vq + 2;

            // ──────────────────────────────────────────────────────────────────
            // Chỉ số 2: Impact Path Length – L_q = V_q - 1
            // ──────────────────────────────────────────────────────────────────
            int impactPathLength = Math.Max(0, activeNodes.Count - 1);

            // ──────────────────────────────────────────────────────────────────
            // Chỉ số 3: Đếm DecisionGateway trong chuỗi Active Nodes
            // ──────────────────────────────────────────────────────────────────
            int gatewaysCount = activeNodes.Count(n =>
                string.Equals(n.NodeType, "DecisionGateway", StringComparison.OrdinalIgnoreCase));

            var gatewayNames = activeNodes
                .Where(n => string.Equals(n.NodeType, "DecisionGateway", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.NodeName)
                .ToList();

            // ──────────────────────────────────────────────────────────────────
            // Phân loại Path Type
            // ──────────────────────────────────────────────────────────────────
            string pathType = ClassifyPathType(gatewaysCount);

            // ──────────────────────────────────────────────────────────────────
            // Phân loại Độ Khó (nhất quán với WorkflowAssessmentService.cs)
            // ──────────────────────────────────────────────────────────────────
            string level = ClassifyDifficulty(gatewaysCount, impactPathLength);

            // ──────────────────────────────────────────────────────────────────
            // Xây dựng Reasoning
            // ──────────────────────────────────────────────────────────────────
            string reasoning = BuildReasoning(
                level, cyclomaticComplexity, impactPathLength,
                gatewaysCount, gatewayNames, vq, eq, pathType);

            _logger.LogInformation(
                "[DifficultyAssessment] Hoàn thành – Level={Level} | V(G)={VG} | L_q={L} | Gateways={G}",
                level, cyclomaticComplexity, impactPathLength, gatewaysCount);

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

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE: Phân loại helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Phân loại loại nhánh kích hoạt dựa trên số DecisionGateway.
        /// <list type="bullet">
        ///   <item>0 gateway → "Happy Path"</item>
        ///   <item>1 gateway → "Single Exception"</item>
        ///   <item>≥2 gateways → "Double Exception"</item>
        /// </list>
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
            string   pathType)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append($"Câu hỏi đạt mức {level.ToUpper()} (Path Type: {pathType}). ");
            sb.Append($"Người trả lời phải tư duy qua chuỗi domino {vq} nút tích cực, ");
            sb.Append($"tạo thành {impactPathLength} bước dịch chuyển logic liên tiếp ");
            sb.Append($"(L_q = V_q - 1 = {vq} - 1 = {impactPathLength}). ");

            // Độ phức tạp tuần hoàn
            sb.Append($"Độ phức tạp tuần hoàn V(G) = E_q - V_q + 2 = {eq} - {vq} + 2 = {cyclomaticComplexity}, ");
            sb.Append($"nghĩa là có {cyclomaticComplexity} kịch bản kiểm thử độc lập. ");

            // Gateways
            if (gatewaysCount == 0)
            {
                sb.Append("Luồng đi thẳng, không qua bất kỳ cổng quyết định nào — đây là Happy Path đơn giản nhất.");
            }
            else if (gatewaysCount == 1)
            {
                sb.Append($"Luồng đi qua đúng 1 cổng quyết định ngoại lệ ");
                if (gatewayNames.Count > 0)
                    sb.Append($"('{gatewayNames[0]}') ");
                sb.Append("— buộc người trả lời hiểu một trường hợp kiểm tra điều kiện nghiệp vụ.");
            }
            else
            {
                sb.Append($"Luồng kích hoạt đồng thời {gatewaysCount} cổng quyết định ngoại lệ");
                if (gatewayNames.Count > 0)
                {
                    sb.Append(": ");
                    sb.Append(string.Join(", ", gatewayNames.Select((n, i) => $"({i + 1}) '{n}'")));
                }
                sb.Append(" — yêu cầu người trả lời hiểu thứ tự ưu tiên và tính xung đột giữa các nhánh rẽ.");
            }

            return sb.ToString();
        }
    }
}
