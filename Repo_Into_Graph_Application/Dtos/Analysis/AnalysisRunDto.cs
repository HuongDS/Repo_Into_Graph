using System;
using System.ComponentModel.DataAnnotations;

namespace Repo_Into_Graph_Application.Dtos.Analysis
{
    // ─── Response DTO ────────────────────────────────────────────────────────────

    /// <summary>
    /// Thông tin trả về của một AnalysisRun (không kèm quan hệ con).
    /// </summary>
    public class AnalysisRunDto
    {
        public Guid Id { get; set; }
        public string RepositoryPath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Repository metadata
        public string? RepoName { get; set; }
        public string? RepoOwner { get; set; }
        public string? RepoDescription { get; set; }
        public string? RepoUrl { get; set; }
        public string? RepoLanguage { get; set; }
        public int? RepoStars { get; set; }
        public bool? IsPublic { get; set; }
        public DateTime? RepoUpdatedAt { get; set; }
    }

    // ─── Create / Update request ─────────────────────────────────────────────────

    /// <summary>
    /// Body dùng cho POST (tạo mới) AnalysisRun.
    /// </summary>
    public class CreateAnalysisRunRequest
    {
        [Required(ErrorMessage = "RepositoryPath không được để trống.")]
        [MaxLength(2000)]
        public string RepositoryPath { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? RepoName { get; set; }

        [MaxLength(255)]
        public string? RepoOwner { get; set; }

        [MaxLength(1000)]
        public string? RepoDescription { get; set; }

        [MaxLength(500)]
        public string? RepoUrl { get; set; }

        [MaxLength(100)]
        public string? RepoLanguage { get; set; }

        [Range(0, int.MaxValue)]
        public int? RepoStars { get; set; }

        public bool? IsPublic { get; set; }

        public DateTime? RepoUpdatedAt { get; set; }
    }

    /// <summary>
    /// Body dùng cho PUT (cập nhật) AnalysisRun.
    /// Chỉ cập nhật các trường metadata, không đổi RepositoryPath.
    /// </summary>
    public class UpdateAnalysisRunRequest
    {
        [MaxLength(255)]
        public string? RepoName { get; set; }

        [MaxLength(255)]
        public string? RepoOwner { get; set; }

        [MaxLength(1000)]
        public string? RepoDescription { get; set; }

        [MaxLength(500)]
        public string? RepoUrl { get; set; }

        [MaxLength(100)]
        public string? RepoLanguage { get; set; }

        [Range(0, int.MaxValue)]
        public int? RepoStars { get; set; }

        public bool? IsPublic { get; set; }

        public DateTime? RepoUpdatedAt { get; set; }
    }

    // ─── Pagination ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả phân trang cho bất kỳ kiểu dữ liệu nào.
    /// </summary>
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}





