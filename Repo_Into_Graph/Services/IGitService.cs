using System.Threading.Tasks;

namespace Repo_Into_Graph.Services
{
    public interface IGitService
    {
        bool IsGitUrl(string path);
        Task<string> CloneRepositoryAsync(string gitUrl);
        void DeleteClonedRepository(string localPath);
    }
}
