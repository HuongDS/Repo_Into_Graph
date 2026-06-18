using Repo_Into_Graph_Application.Dtos.FewShot;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.FewShot
{
    public interface IFewShotService
    {
        /// <summary>
        /// Lấy danh sách few-shot examples có phân trang, lọc theo difficulty và tag.
        /// </summary>
        Task<FewShotPagedResult> GetPagedAsync(
            int page,
            int pageSize,
            string? difficulty,
            string? tag);

        /// <summary>
        /// Lấy chi tiết một few-shot example theo ID.
        /// </summary>
        Task<FewShotExampleDto?> GetByIdAsync(Guid id);

        /// <summary>
        /// Tạo mới một few-shot example.
        /// </summary>
        Task<FewShotExampleDto> CreateAsync(CreateFewShotExampleRequest request);

        /// <summary>
        /// Cập nhật thông tin few-shot example. Trả về null nếu không tìm thấy.
        /// </summary>
        Task<FewShotExampleDto?> UpdateAsync(Guid id, UpdateFewShotExampleRequest request);

        /// <summary>
        /// Xóa một few-shot example. Trả về false nếu không tìm thấy.
        /// </summary>
        Task<bool> DeleteAsync(Guid id);
    }
}





