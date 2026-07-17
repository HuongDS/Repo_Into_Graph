namespace Repo_Into_Graph_DataAccess.Consts
{
    /// <summary>
    /// Phân loại nút trong đồ thị luồng nghiệp vụ.
    /// DecisionGateway → nút rẽ nhánh (if/switch) → ảnh hưởng đến Độ Khó.
    /// </summary>
    public enum NodeType
    {
        /// <summary>Bước xử lý thông thường (action, task, …).</summary>
        Activity,

        /// <summary>Điểm bắt đầu của luồng.</summary>
        StartEvent,

        /// <summary>Điểm kết thúc của luồng.</summary>
        EndEvent,

        /// <summary>Nút rẽ nhánh điều kiện – dùng để tính Độ Khó.</summary>
        DecisionGateway,

        /// <summary>Nút hội tụ nhiều nhánh lại.</summary>
        MergeGateway,
    }
}
