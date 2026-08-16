using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class updateFullDatabase : Migration
    {
        // Migration này ban đầu được scaffold sai: model snapshot lúc generate bị đứt gãy khỏi lịch sử
        // migration trước đó (20260620005557/20260620031858), nên EF tưởng database đang trống và sinh
        // lại CREATE TABLE cho toàn bộ 9 bảng vốn đã tồn tại từ 2 migration trước -> luôn fail với lỗi
        // "relation already exists" trên mọi database (kể cả database mới tinh), chặn đứng toàn bộ các
        // migration phía sau (Add_NodeType_To_MethodSource, AddGlobalNodeCount, AddConditionContext).
        // Thay đổi thực duy nhất migration này định mang theo (Difficulty: varchar -> int) đã bị revert
        // trong model hiện tại (FewShotExample.Difficulty vẫn là string, xem AnalysisDbContext.cs) nên
        // so với schema thực tế sau 20260620031858, migration này không có thay đổi nào cần áp dụng.
        // => giữ nguyên migration trong lịch sử (để không phá __EFMigrationsHistory ở các môi trường
        // khác) nhưng Up/Down không làm gì cả.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
