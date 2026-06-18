using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Repo_Into_Graph_Application.Dtos.FewShot
{
    // ─── Response DTO ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Thông tin trả về của một FewShotExample.
    /// </summary>
    public class FewShotExampleDto
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string SuggestedAnswer { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public string? Tag { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ─── Create request ───────────────────────────────────────────────────────────

    /// <summary>
    /// Body cho POST (tạo mới) FewShotExample.
    /// </summary>
    public class CreateFewShotExampleRequest
    {
        [Required(ErrorMessage = "Question không được để trống.")]
        [MaxLength(2000)]
        public string Question { get; set; } = string.Empty;

        [Required(ErrorMessage = "SuggestedAnswer không được để trống.")]
        public string SuggestedAnswer { get; set; } = string.Empty;

        [Required(ErrorMessage = "Difficulty không được để trống.")]
        [MaxLength(20)]
        public string Difficulty { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Tag { get; set; }

        public string? Description { get; set; }
    }

    // ─── Update request ───────────────────────────────────────────────────────────

    /// <summary>
    /// Body cho PUT (cập nhật) FewShotExample.
    /// Các trường null sẽ giữ nguyên giá trị cũ.
    /// </summary>
    public class UpdateFewShotExampleRequest
    {
        [MaxLength(2000)]
        public string? Question { get; set; }

        public string? SuggestedAnswer { get; set; }

        [MaxLength(20)]
        public string? Difficulty { get; set; }

        [MaxLength(100)]
        public string? Tag { get; set; }

        public string? Description { get; set; }
    }

    // ─── Paged result ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả phân trang cho FewShotExampleDto.
    /// </summary>
    public class FewShotPagedResult
    {
        public IEnumerable<FewShotExampleDto> Items { get; set; }
            = Enumerable.Empty<FewShotExampleDto>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}





