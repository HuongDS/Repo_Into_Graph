using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Services
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

        public async Task<string> CloneRepositoryAsync(string gitUrl)
        {
            string tempDirName = $"temp_cloned_{Guid.NewGuid()}";
            string targetPath = Path.Combine(Directory.GetCurrentDirectory(), "temp_repos", tempDirName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "git";
                process.StartInfo.Arguments = $"clone --depth 1 {gitUrl} \"{targetPath}\"";
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
