using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.GitService
{
    public class GitService : IGitService
    {
        public bool IsGitUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string trimmed = path.Trim();
            return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Chuyển đổi URL trình duyệt GitHub/GitLab sang dạng git clone URL.
        /// Ví dụ: https://github.com/user/repo/tree/main -> https://github.com/user/repo.git
        /// </summary>
        public string NormalizeGitUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            string trimmed = url.Trim();

            // Nếu đã có .git ở cuối thì giữ nguyên
            if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            // Nhận dạng GitHub browser URL: https://github.com/{owner}/{repo}[/...]
            var githubMatch = Regex.Match(trimmed,
                @"^https?://github\.com/([^/]+/[^/]+?)(?:/.*)?$",
                RegexOptions.IgnoreCase);
            if (githubMatch.Success)
                return $"https://github.com/{githubMatch.Groups[1].Value}.git";

            // Nhận dạng GitLab browser URL: https://gitlab.com/{owner}/{repo}[/...]
            var gitlabMatch = Regex.Match(trimmed,
                @"^https?://gitlab\.com/([^/]+/[^/]+?)(?:/.*)?$",
                RegexOptions.IgnoreCase);
            if (gitlabMatch.Success)
                return $"https://gitlab.com/{gitlabMatch.Groups[1].Value}.git";

            // Nhận dạng Bitbucket browser URL
            var bitbucketMatch = Regex.Match(trimmed,
                @"^https?://bitbucket\.org/([^/]+/[^/]+?)(?:/.*)?$",
                RegexOptions.IgnoreCase);
            if (bitbucketMatch.Success)
                return $"https://bitbucket.org/{bitbucketMatch.Groups[1].Value}.git";

            // Trả về nguyên bản nếu không khớp
            return trimmed;
        }

        private string ExtractSubfolderPath(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            
            // Match GitHub URL: https://github.com/owner/repo/tree/branch/subfolder
            var githubMatch = Regex.Match(url.Trim(), @"^https?://github\.com/[^/]+/[^/]+/tree/[^/]+/(.+)$", RegexOptions.IgnoreCase);
            if (githubMatch.Success)
            {
                return githubMatch.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
            }
            return string.Empty;
        }

        public async Task<string> CloneRepositoryAsync(string gitUrl)
        {
            string subfolder = ExtractSubfolderPath(gitUrl);
            
            // Normalize URL từ trình duyệt (GitHub/GitLab browser link) sang git clone URL
            string cloneUrl = NormalizeGitUrl(gitUrl);

            string tempDirName = $"temp_cloned_{Guid.NewGuid()}";
            string targetPath = Path.Combine(Directory.GetCurrentDirectory(), "temp_repos", tempDirName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "git";
                process.StartInfo.Arguments = $"clone --depth 1 \"{cloneUrl}\" \"{targetPath}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    throw new InvalidOperationException($"Lỗi lệnh clone: {error}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi khi thực thi git clone: {ex.Message}", ex);
            }

            if (!string.IsNullOrEmpty(subfolder))
            {
                string fullSubfolderPath = Path.Combine(targetPath, subfolder);
                if (Directory.Exists(fullSubfolderPath))
                {
                    return fullSubfolderPath;
                }
            }

            return targetPath;
        }

        public void DeleteClonedRepository(string localPath)
        {
            // Nếu targetPath truyền vào là 1 subfolder bên trong repo (trường hợp URL GitHub trỏ tới
            // /tree/branch/subfolder), phải xóa từ gốc thư mục temp_cloned_* chứ không chỉ xóa subfolder,
            // nếu không phần .git/… ở gốc sẽ bị bỏ sót và tồn tại vĩnh viễn trong temp_repos.
            string rootToDelete = FindTempClonedRoot(localPath) ?? localPath;

            if (!Directory.Exists(rootToDelete)) return;

            try
            {
                // git clone trên Windows set thuộc tính ReadOnly cho các file trong .git/objects,
                // khiến Directory.Delete(recursive: true) ném UnauthorizedAccessException và bị nuốt
                // ở catch bên dưới -> thư mục tạm bị kẹt lại vĩnh viễn trong temp_repos. Phải gỡ ReadOnly
                // trước khi xóa.
                ClearReadOnlyAttributes(rootToDelete);
                Directory.Delete(rootToDelete, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Không thể xóa thư mục tạm: {ex.Message}");
            }
        }

        private static string? FindTempClonedRoot(string path)
        {
            var dir = new DirectoryInfo(path);
            while (dir != null)
            {
                if (dir.Name.StartsWith("temp_cloned_", StringComparison.OrdinalIgnoreCase))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private static void ClearReadOnlyAttributes(string rootPath)
        {
            var root = new DirectoryInfo(rootPath);
            root.Attributes = FileAttributes.Normal;

            foreach (var dir in root.GetDirectories("*", SearchOption.AllDirectories))
                dir.Attributes = FileAttributes.Normal;

            foreach (var file in root.GetFiles("*", SearchOption.AllDirectories))
                file.Attributes = FileAttributes.Normal;
        }
    }
}





