using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Lap rap "Ngu canh Lai" thanh khoi van ban san sang nap vao prompt cua Tang 3.
    ///
    /// Bo cuc:
    ///   ### 1. SYSTEM WORKFLOW GRAPH (CFG SKELETON)   -> so do Mermaid
    ///   ### 2. CRITICAL DECISION LOGIC (GROUND TRUTH) -> code nguyen van, gan theo node
    ///   ### 3. ENRICHED METADATA                      -> chi xuat hien khi co du lieu
    /// </summary>
    internal static class HybridPromptComposer
    {
        /// <summary>
        /// Nguong trong so de mot snippet duoc dua vao prompt.
        /// Duoi nguong (vd loi goi phu thuoc thong thuong) da duoc CFG mo ta du roi,
        /// van con nguyen trong truong criticalSnippets cua response.
        /// </summary>
        private const int PromptWeightThreshold = 7;

        public static string Compose(string cfgSkeleton, EnrichedMetadataDto metadata,
            List<CriticalSnippetDto> snippets)
        {
            var sb = new StringBuilder();

            sb.AppendLine("### 1. SYSTEM WORKFLOW GRAPH (CFG SKELETON)");
            sb.AppendLine("```mermaid");
            sb.AppendLine(cfgSkeleton);
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("### 2. CRITICAL DECISION LOGIC (GROUND TRUTH)");

            // Chi giu lai cac doan thuc su quyet dinh luong xu ly (nhanh, throw, catch, loop, switch, async)
            var promptSnippets = snippets.Where(s => s.Weight >= PromptWeightThreshold).ToList();
            if (promptSnippets.Count == 0) promptSnippets = snippets;

            if (promptSnippets.Count == 0)
            {
                sb.AppendLine("- (khong co nhanh dieu kien / ngoai le nao can giu nguyen van)");
            }
            else
            {
                foreach (var snippet in promptSnippets)
                {
                    string anchor = string.IsNullOrEmpty(snippet.NodeId)
                        ? "Line " + snippet.Line
                        : "Node " + snippet.NodeId;
                    sb.AppendLine("- **" + anchor + ":** " + SourceScanner.Collapse(snippet.Code));
                }
            }

            string metadataBlock = ComposeMetadata(metadata);
            if (!string.IsNullOrEmpty(metadataBlock))
            {
                sb.AppendLine();
                sb.AppendLine("### 3. ENRICHED METADATA");
                sb.Append(metadataBlock);
            }

            return sb.ToString().TrimEnd();
        }

        private static string ComposeMetadata(EnrichedMetadataDto metadata)
        {
            var lines = new List<string>();

            foreach (var method in metadata.Methods.Take(5))
            {
                lines.Add("- **Method:** " + method.Name + "(" + method.Parameters + ")" +
                          (string.IsNullOrEmpty(method.ReturnType) ? string.Empty : " : " + method.ReturnType) +
                          (method.IsAsync ? " `async`" : string.Empty) +
                          (method.IsStatic ? " `static`" : string.Empty));
            }

            if (metadata.IsAsync)
            {
                var markers = metadata.AsyncMarkers
                    .Select(m => m.Marker + "@L" + m.Line)
                    .Distinct(StringComparer.Ordinal)
                    .Take(8);
                lines.Add("- **Async:** YES (" + string.Join(", ", markers) + ")");
            }

            if (metadata.AnnotationTags.Count > 0)
            {
                lines.Add("- **Annotations:** " + string.Join(", ", metadata.AnnotationTags.Take(15)));
            }

            if (metadata.Dependencies.Count > 0)
            {
                lines.Add("- **Dependencies:** " +
                          string.Join(", ", metadata.Dependencies.Take(12).Select(FormatDependency)));
            }

            if (metadata.ThrownExceptions.Count > 0)
            {
                lines.Add("- **Throws:** " + string.Join(", ", metadata.ThrownExceptions.Take(10)));
            }

            if (metadata.CaughtExceptions.Count > 0)
            {
                lines.Add("- **Catches:** " + string.Join(", ", metadata.CaughtExceptions.Take(10)));
            }

            var flow = metadata.ControlFlow;
            if (flow.BranchCount + flow.LoopCount + flow.SwitchCount + flow.TryCatchCount +
                flow.ThrowCount + flow.ReturnCount > 0)
            {
                lines.Add("- **Control flow:** branch=" + flow.BranchCount +
                          ", loop=" + flow.LoopCount +
                          ", switch=" + flow.SwitchCount +
                          ", try/catch=" + flow.TryCatchCount +
                          ", throw=" + flow.ThrowCount +
                          ", return=" + flow.ReturnCount +
                          ", maxNesting=" + flow.MaxNestingDepth);
            }

            if (metadata.Tags.Count > 0)
            {
                lines.Add("- **Tags:** " + string.Join(", ", metadata.Tags.Take(20)));
            }

            if (lines.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach (var line in lines) sb.AppendLine(line);
            return sb.ToString();
        }

        private static string FormatDependency(DependencyDto dependency)
        {
            string members = dependency.Members.Count > 0
                ? "." + string.Join("/", dependency.Members.Take(5))
                : string.Empty;
            return dependency.Name + members + " (" + dependency.Kind + " x" + dependency.UsageCount + ")";
        }
    }
}
