using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Trich xuat Enriched Metadata: async/await markers, annotation tags, dependency info,
    /// exception, thong ke luong dieu khien va cac tag ngu nghia.
    /// </summary>
    internal sealed class EnrichedMetadataExtractor
    {
        private static readonly HashSet<string> IgnoredReceivers = new(StringComparer.Ordinal)
        {
            "this", "base", "super", "if", "for", "while", "switch", "return", "new",
            "catch", "foreach", "using", "lock", "do", "else", "try"
        };

        private static readonly (string Marker, string Pattern)[] AsyncPatterns =
        {
            ("ASYNC_MODIFIER", @"\basync\b"),
            ("AWAIT",          @"\bawait\b"),
            ("ASYNC_CALL",     @"\b\w*Async\s*\("),
            ("TASK",           @"\bTask\s*[<\.]|\bValueTask\b|\bTask\.Run\b|\bConfigureAwait\b|\bGetAwaiter\b"),
            ("FUTURE",         @"\bCompletableFuture\b|\bFuture\s*<|\bExecutorService\b|\bExecutors\.|\bnew\s+Thread\b|\bThreadPool\b"),
            ("ANNOTATION",     @"@Async\b|@Scheduled\b|@EnableAsync\b"),
            ("REACTIVE",       @"\bMono\s*<|\bFlux\s*<|\.subscribe\s*\(|\bObservable\b|\bIAsyncEnumerable\b")
        };

        private readonly SourceScanner _sc;
        private readonly List<MethodDecl> _methods;

        public EnrichedMetadataExtractor(SourceScanner scanner, List<MethodDecl> methods)
        {
            _sc = scanner;
            _methods = methods;
        }

        public EnrichedMetadataDto Extract(ControlFlowStatsDto stats)
        {
            var metadata = new EnrichedMetadataDto
            {
                ControlFlow = stats,
                AsyncMarkers = ExtractAsyncMarkers(),
                AnnotationTags = ExtractAnnotationTags(),
                Dependencies = ExtractDependencies(),
                ThrownExceptions = ExtractThrownExceptions(),
                CaughtExceptions = ExtractCaughtExceptions(),
                Methods = _methods.Where(m => !m.IsSynthetic).Select(ToSignatureDto).ToList()
            };

            metadata.IsAsync = metadata.AsyncMarkers.Count > 0 || _methods.Any(m => m.IsAsync);
            metadata.Tags = BuildTags(metadata, stats);
            return metadata;
        }

        private static MethodSignatureDto ToSignatureDto(MethodDecl method)
        {
            return new MethodSignatureDto
            {
                Name = method.Name,
                ReturnType = method.ReturnType,
                Parameters = method.Parameters,
                Modifiers = method.Modifiers,
                Annotations = method.Annotations,
                IsAsync = method.IsAsync,
                IsStatic = method.IsStatic,
                StartLine = method.StartLine
            };
        }

        // -------------------------------------------------------------
        private List<AsyncMarkerDto> ExtractAsyncMarkers()
        {
            var markers = new List<AsyncMarkerDto>();
            string[] maskedLines = _sc.Masked.Replace("\r\n", "\n").Split('\n');
            string[] sourceLines = _sc.Source.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < maskedLines.Length; i++)
            {
                foreach (var (marker, pattern) in AsyncPatterns)
                {
                    if (!Regex.IsMatch(maskedLines[i], pattern)) continue;

                    string code = i < sourceLines.Length ? sourceLines[i].Trim() : string.Empty;
                    markers.Add(new AsyncMarkerDto
                    {
                        Marker = marker,
                        Line = i + 1,
                        Code = code.Length > 160 ? code.Substring(0, 157) + "..." : code
                    });
                }
            }
            return markers;
        }

        private List<string> ExtractAnnotationTags()
        {
            var tags = new List<string>();

            foreach (Match match in Regex.Matches(_sc.Masked, @"@([A-Za-z_]\w*)"))
            {
                string tag = "@" + match.Groups[1].Value;
                if (!tags.Contains(tag)) tags.Add(tag);
            }

            foreach (Match match in Regex.Matches(_sc.Masked, @"(?m)^\s*\[\s*([A-Za-z_][\w.]*)"))
            {
                string tag = "[" + match.Groups[1].Value + "]";
                if (!tags.Contains(tag)) tags.Add(tag);
            }

            return tags;
        }

        private List<DependencyDto> ExtractDependencies()
        {
            var map = new Dictionary<string, DependencyDto>(StringComparer.Ordinal);

            void Register(string name, string kind, string member)
            {
                if (string.IsNullOrEmpty(name) || IgnoredReceivers.Contains(name)) return;

                if (!map.TryGetValue(name, out var dependency))
                {
                    dependency = new DependencyDto { Name = name, Kind = kind };
                    map[name] = dependency;
                }
                dependency.UsageCount++;
                if (!string.IsNullOrEmpty(member) && !dependency.Members.Contains(member))
                {
                    dependency.Members.Add(member);
                }
            }

            // obj.method(...) hoac ClassName.method(...)
            foreach (Match match in Regex.Matches(_sc.Masked, @"([A-Za-z_]\w*)\s*\.\s*([A-Za-z_]\w*)\s*\("))
            {
                string receiver = match.Groups[1].Value;
                string member = match.Groups[2].Value;
                bool isStatic = char.IsUpper(receiver[0]);
                Register(receiver, isStatic ? "STATIC_CALL" : "FIELD_CALL", member);
            }

            // new ClassName(...)
            foreach (Match match in Regex.Matches(_sc.Masked, @"\bnew\s+([A-Za-z_][\w.]*)\s*[\(<]"))
            {
                string type = match.Groups[1].Value;
                int dot = type.LastIndexOf('.');
                if (dot >= 0) type = type.Substring(dot + 1);
                Register(type, "INSTANTIATION", string.Empty);
            }

            // Kieu du lieu cua tham so dau vao
            foreach (var method in _methods)
            {
                foreach (var parameter in SplitParameters(method.Parameters))
                {
                    var tokens = parameter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length < 2) continue;

                    string type = tokens[tokens.Length - 2];
                    type = Regex.Replace(type, @"<.*?>|\[\]", string.Empty);
                    if (type.Length > 0 && char.IsUpper(type[0])) Register(type, "PARAMETER", string.Empty);
                }
            }

            return map.Values.OrderByDescending(d => d.UsageCount).ThenBy(d => d.Name, StringComparer.Ordinal).ToList();
        }

        private static IEnumerable<string> SplitParameters(string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters)) yield break;

            int depth = 0;
            var current = new System.Text.StringBuilder();
            foreach (char c in parameters)
            {
                if (c == '<' || c == '(' || c == '[') depth++;
                else if (c == '>' || c == ')' || c == ']') depth--;

                if (c == ',' && depth <= 0)
                {
                    yield return current.ToString().Trim();
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) yield return current.ToString().Trim();
        }

        private List<string> ExtractThrownExceptions()
        {
            var list = new List<string>();

            foreach (Match match in Regex.Matches(_sc.Masked, @"throw\s+new\s+([A-Za-z_][\w.]*)"))
            {
                string type = match.Groups[1].Value;
                int dot = type.LastIndexOf('.');
                if (dot >= 0) type = type.Substring(dot + 1);
                if (!list.Contains(type)) list.Add(type);
            }

            foreach (var method in _methods)
            {
                foreach (var type in method.ThrowsTypes)
                {
                    if (!list.Contains(type)) list.Add(type);
                }
            }

            return list;
        }

        private List<string> ExtractCaughtExceptions()
        {
            var list = new List<string>();
            foreach (Match match in Regex.Matches(_sc.Masked, @"catch\s*\(\s*([A-Za-z_][\w.]*)"))
            {
                string type = match.Groups[1].Value;
                int dot = type.LastIndexOf('.');
                if (dot >= 0) type = type.Substring(dot + 1);
                if (!list.Contains(type)) list.Add(type);
            }
            return list;
        }

        private List<string> BuildTags(EnrichedMetadataDto metadata, ControlFlowStatsDto stats)
        {
            var tags = new List<string>();

            void Add(string tag)
            {
                if (!tags.Contains(tag)) tags.Add(tag);
            }

            if (metadata.IsAsync) Add("async");
            if (stats.LoopCount > 0) Add("has-loop");
            if (stats.HasComplexLoop) Add("complex-loop");
            if (stats.BranchCount > 0) Add("has-branch");
            if (stats.ThrowCount > 0) Add("throws-exception");
            if (stats.TryCatchCount > 0) Add("has-try-catch");
            if (stats.SwitchCount > 0) Add("has-switch");
            if (stats.MaxNestingDepth >= 3) Add("deep-nesting");
            if (metadata.Dependencies.Count == 0) Add("independent");

            foreach (var dependency in metadata.Dependencies)
            {
                string name = dependency.Name.ToLowerInvariant();
                string members = string.Join(" ", dependency.Members).ToLowerInvariant();

                if (name.Contains("cache") || members.Contains("cache")) Add("cache");
                if (name.Contains("repository") || name.Contains("repo") || name.Contains("dao") ||
                    name.Contains("dbcontext") || name.Contains("entitymanager")) Add("database");
                if (name.Contains("http") || name.Contains("client") || name.Contains("rest") ||
                    name.Contains("feign")) Add("external-api");
                if (name.Contains("mail") || name.Contains("email") || name.Contains("notif") ||
                    name.Contains("sms")) Add("notification");
                if (name.Contains("file") || name.Contains("stream") || name.Contains("io")) Add("io");
                if (name.Contains("service")) Add("service-call");
            }

            foreach (var tag in metadata.AnnotationTags)
            {
                string lower = tag.ToLowerInvariant();
                if (lower.Contains("transactional")) Add("transactional");
                if (lower.Contains("test")) Add("test-code");
                if (lower.Contains("override")) Add("override");
                if (lower.Contains("http") || lower.Contains("mapping") || lower.Contains("route")) Add("endpoint");
            }

            return tags;
        }
    }
}
