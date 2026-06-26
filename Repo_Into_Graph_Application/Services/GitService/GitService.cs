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

     
            string baseTempPath = Path.Combine(Path.GetTempPath(), "Repo_Into_Graph_API", "temp_repos");

            string targetPath = Path.Combine(baseTempPath, tempDirName);
            Directory.CreateDirectory(targetPath);

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
            if (Directory.Exists(localPath))
            {
                try
                {
                    Directory.Delete(localPath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Không thể xóa thư mục tạm: {ex.Message}");
                }
            }
        }
    }
}





