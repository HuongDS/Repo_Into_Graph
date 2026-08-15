using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Helper
{
    public static class SplitCamelCase
    {
        public static IEnumerable<string> Split(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) yield break;
            var current = new StringBuilder();
            foreach (char c in name)
            {
                if (char.IsUpper(c) && current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                current.Append(c);
            }
            if (current.Length > 0) yield return current.ToString();
        }
    }
}
