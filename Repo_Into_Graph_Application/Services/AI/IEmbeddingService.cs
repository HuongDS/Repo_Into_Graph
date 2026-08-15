using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.AI
{
    public interface IEmbeddingService
    {
        Task<double[][]> EmbedBatchAsync(List<string> texts, string inputType);
        double CosineSimilarity(double[] a, double[] b);
    }
}
