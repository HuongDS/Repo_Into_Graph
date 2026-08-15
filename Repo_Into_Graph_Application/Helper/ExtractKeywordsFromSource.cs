using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Helper
{
    public static class ExtractKeywordsFromSource
    {
        public static string Extract(string sourceCode)
        {
            if (string.IsNullOrWhiteSpace(sourceCode)) return string.Empty;
            return sourceCode.Length > 300 ? sourceCode[..300] : sourceCode;
        }
    }
}
