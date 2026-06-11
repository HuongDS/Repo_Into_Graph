using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Repo_Into_Graph.Models;

namespace Repo_Into_Graph.Services.Parsers;

/// <summary>
/// C# parser using Roslyn for full semantic analysis.
/// Handles .NET API projects (ASP.NET Core, Web API, MVC).
/// </summary>
public class CSharpParser : ILanguageParser
{
    public string LanguageName => "C# (.NET)";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".cs" };

    private readonly HashSet<string> _standardNamespaces = new()
    {
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
        "System.IO",
        "System.Threading",
        "System.Diagnostics",
        "System.Net",
        "System.Reflection"
    };

    private static readonly HashSet<string> _skipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj", "bin", "node_modules", ".git", ".github", ".vscode", ".idea",
        "__pycache__", ".pytest_cache", ".mypy_cache", "venv", ".venv", "env",
        "dist", "build", ".next", ".nuxt", "coverage", "migrations", "Migrations",
        "target"
    };

    private Compilation? _compilation;
    private readonly Dictionary<string, SyntaxTree> _syntaxTrees = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? _interfaceImplementationMap;
    private readonly object _lock = new();

    private static string? FindRepositoryRoot(string startPath)
    {
        var dir = Path.GetDirectoryName(startPath);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) || 
                Directory.GetFiles(dir, "*.sln").Any() || 
                Directory.GetFiles(dir, "*.csproj").Any())
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return Path.GetDirectoryName(startPath);
    }

    private static bool IsInSkippedDirectory(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => _skipDirs.Contains(part));
    }

    private void InitializeCompilation(string filePath)
    {
        lock (_lock)
        {
            if (_compilation != null) return;

            var rootDir = FindRepositoryRoot(filePath);
            if (rootDir == null) return;

            var csFiles = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !IsInSkippedDirectory(f))
                .ToList();

            var trees = new List<SyntaxTree>();
            foreach (var file in csFiles)
            {
                try
                {
                    var code = File.ReadAllText(file);
                    var tree = CSharpSyntaxTree.ParseText(code, path: file);
                    _syntaxTrees[file] = tree;
                    trees.Add(tree);
                }
                catch
                {
                    // Ignore read errors
                }
            }

            _compilation = CSharpCompilation.Create("CSharpAnalysis")
                .AddSyntaxTrees(trees)
                .AddReferences(GetReferenceAssemblies());

            _interfaceImplementationMap = BuildInterfaceImplementationMap(_compilation);
        }
    }

    public Task<ExtractionResult> ParseAsync(string filePath, string sourceCode)
    {
        var result = new ExtractionResult();

        try
        {
            InitializeCompilation(filePath);

            if (_compilation == null)
                return Task.FromResult(result);

            if (!_syntaxTrees.TryGetValue(filePath, out var syntaxTree))
            {
                syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: filePath);
                _compilation = _compilation.AddSyntaxTrees(syntaxTree);
                _syntaxTrees[filePath] = syntaxTree;
            }

            var semanticModel = _compilation.GetSemanticModel(syntaxTree);
            var root = (CompilationUnitSyntax)syntaxTree.GetRoot();
            var visitor = new CallGraphVisitor(semanticModel, _standardNamespaces, _interfaceImplementationMap, LanguageName);
            visitor.Visit(root);

            result.CallGraphEdges = visitor.CallGraphEdges;
            result.MethodSources = visitor.MethodSources;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  C# parse error in {Path.GetFileName(filePath)}: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    private static List<MetadataReference> GetReferenceAssemblies()
    {
        var references = new List<MetadataReference>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (!assembly.IsCollectible && !string.IsNullOrEmpty(assembly.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
            catch
            {
                // Ignore assemblies that cannot be loaded
            }
        }
        return references;
    }

    private static Dictionary<IMethodSymbol, List<IMethodSymbol>> BuildInterfaceImplementationMap(Compilation compilation)
    {
        var map = new Dictionary<IMethodSymbol, List<IMethodSymbol>>(SymbolEqualityComparer.Default);

        foreach (var namedType in GetAllNamedTypes(compilation.Assembly.GlobalNamespace))
        {
            if (namedType.TypeKind != TypeKind.Class || namedType.IsAbstract)
                continue;

            foreach (var interfaceType in namedType.AllInterfaces)
            {
                foreach (var interfaceMember in interfaceType.GetMembers().OfType<IMethodSymbol>())
                {
                    var implementation = namedType.FindImplementationForInterfaceMember(interfaceMember) as IMethodSymbol;
                    if (implementation == null) continue;

                    var interfaceMethod = interfaceMember.OriginalDefinition;
                    if (!map.TryGetValue(interfaceMethod, out var implementations))
                    {
                        implementations = new List<IMethodSymbol>();
                        map[interfaceMethod] = implementations;
                    }

                    if (!implementations.Any(e => SymbolEqualityComparer.Default.Equals(e, implementation)))
                        implementations.Add(implementation);
                }
            }
        }

        return map;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
                yield return nested;
        }
        foreach (var nestedNs in ns.GetNamespaceMembers())
            foreach (var type in GetAllNamedTypes(nestedNs))
                yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in GetNestedTypes(nested))
                yield return deeper;
        }
    }

    // ─── Inner Syntax Walker ───────────────────────────────────────────────────

    private class CallGraphVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly HashSet<string> _standardNamespaces;
        private readonly IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? _interfaceImplementationMap;
        private readonly string _language;
        private string _currentClass = string.Empty;
        private string _currentMethod = string.Empty;
        private string _currentMethodDisplay = string.Empty;

        public List<CallGraphEdge> CallGraphEdges { get; } = new();
        public List<MethodSource> MethodSources { get; } = new();

        public CallGraphVisitor(
            SemanticModel semanticModel,
            HashSet<string> standardNamespaces,
            IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? interfaceImplementationMap,
            string language)
        {
            _semanticModel = semanticModel;
            _standardNamespaces = standardNamespaces;
            _interfaceImplementationMap = interfaceImplementationMap;
            _language = language;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var prev = _currentClass;
            _currentClass = node.Identifier.Text;
            base.VisitClassDeclaration(node);
            _currentClass = prev;
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            var prev = _currentClass;
            _currentClass = node.Identifier.Text;
            base.VisitInterfaceDeclaration(node);
            _currentClass = prev;
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            var prev = _currentClass;
            _currentClass = node.Identifier.Text;
            base.VisitRecordDeclaration(node);
            _currentClass = prev;
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            var prev = _currentClass;
            _currentClass = node.Identifier.Text;
            base.VisitStructDeclaration(node);
            _currentClass = prev;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var prevMethod = _currentMethod;
            var prevDisplay = _currentMethodDisplay;
            _currentMethod = node.Identifier.Text;
            var verb = node.AttributeLists
                .SelectMany(a => a.Attributes)
                .Select(a => GetHttpVerb(a.Name.ToString()))
                .FirstOrDefault(v => !string.IsNullOrEmpty(v));
            var displayName = string.IsNullOrEmpty(verb) ? _currentMethod : $"{verb} {_currentMethod}";
            _currentMethodDisplay = displayName;

            if (!IsMigrationClass(_currentClass))
            {
                MethodSources.Add(new MethodSource
                {
                    ClassName = _currentClass,
                    MethodName = _currentMethod,
                    SourceCode = node.ToString(),
                    Language = _language,
                    HttpVerb = string.IsNullOrEmpty(verb) ? null : verb,
                    DisplayName = displayName
                });

                if (ShouldIncludeMethodEntry(node))
                {
                    CallGraphEdges.Add(new CallGraphEdge
                    {
                        CallerClass = _currentClass,
                        CallerMethod = "__CLASS__",
                        CalleeClass = _currentClass,
                        CalleeMethod = _currentMethod,
                        Language = _language,
                        CallerDisplayName = "__CLASS__",
                        CalleeDisplayName = displayName
                    });
                }
            }

            base.VisitMethodDeclaration(node);
            _currentMethod = prevMethod;
            _currentMethodDisplay = prevDisplay;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (_currentClass != string.Empty && _currentMethod != string.Empty && !IsMigrationClass(_currentClass))
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(node);
                var methodSymbol = ResolveMethodSymbol(symbolInfo);

                if (methodSymbol != null)
                {
                    var calleeClass = methodSymbol.ContainingType?.Name ?? "Unknown";
                    var calleeName = methodSymbol.Name;

                    if (!IsStandardLibraryCall(methodSymbol) && !IsMigrationClass(calleeClass) && !IsMigration(methodSymbol))
                    {
                        CallGraphEdges.Add(new CallGraphEdge
                        {
                            CallerClass = _currentClass,
                            CallerMethod = _currentMethod,
                            CalleeClass = calleeClass,
                            CalleeMethod = calleeName,
                            Language = _language,
                            CallerDisplayName = _currentMethodDisplay,
                            CalleeDisplayName = calleeName
                        });

                        AddImplementationEdges(methodSymbol);
                    }
                }
            }

            base.VisitInvocationExpression(node);
        }

        private IMethodSymbol? ResolveMethodSymbol(SymbolInfo symbolInfo)
        {
            if (symbolInfo.Symbol is IMethodSymbol direct) return direct;
            if (symbolInfo.Symbol is IPropertySymbol prop && prop.GetMethod is IMethodSymbol getter) return getter;
            foreach (var candidate in symbolInfo.CandidateSymbols)
            {
                if (candidate is IMethodSymbol m) return m;
                if (candidate is IPropertySymbol p && p.GetMethod is IMethodSymbol g) return g;
            }
            return null;
        }

        private void AddImplementationEdges(IMethodSymbol interfaceMethod)
        {
            if (_interfaceImplementationMap == null) return;
            var containingType = interfaceMethod.ContainingType;
            if (containingType == null || containingType.TypeKind != TypeKind.Interface) return;
            if (!_interfaceImplementationMap.TryGetValue(interfaceMethod.OriginalDefinition, out var implementations)) return;

            foreach (var impl in implementations)
            {
                var calleeClass = impl.ContainingType?.Name ?? "Unknown";
                if (!IsMigrationClass(containingType.Name) && !IsMigrationClass(calleeClass) && !IsMigration(impl))
                {
                    CallGraphEdges.Add(new CallGraphEdge
                    {
                        CallerClass = containingType.Name,
                        CallerMethod = interfaceMethod.Name,
                        CalleeClass = calleeClass,
                        CalleeMethod = impl.Name,
                        Language = _language,
                        CallerDisplayName = interfaceMethod.Name,
                        CalleeDisplayName = impl.Name
                    });
                }
            }
        }

        private bool ShouldIncludeMethodEntry(MethodDeclarationSyntax node)
        {
            var hasHttpAttr = node.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(a => IsHttpAttribute(a.Name.ToString()));

            var isControllerMethod = _currentClass.EndsWith("Controller", StringComparison.Ordinal)
                                     && node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
            return hasHttpAttr || isControllerMethod;
        }

        private string GetMethodDisplayName(MethodDeclarationSyntax node)
        {
            var verb = node.AttributeLists
                .SelectMany(a => a.Attributes)
                .Select(a => GetHttpVerb(a.Name.ToString()))
                .FirstOrDefault(v => !string.IsNullOrEmpty(v));

            return string.IsNullOrEmpty(verb) ? node.Identifier.Text : $"{verb} {node.Identifier.Text}";
        }

        private static bool IsHttpAttribute(string name)
        {
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
                name = name[..^"Attribute".Length];
            return name is "HttpGet" or "Get" or "HttpPost" or "Post"
                       or "HttpPut" or "Put" or "HttpDelete" or "Delete" or "HttpPatch" or "Patch";
        }

        private static string GetHttpVerb(string name)
        {
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
                name = name[..^"Attribute".Length];
            return name switch
            {
                "HttpGet" or "Get" => "GET",
                "HttpPost" or "Post" => "POST",
                "HttpPut" or "Put" => "PUT",
                "HttpDelete" or "Delete" => "DELETE",
                "HttpPatch" or "Patch" => "PATCH",
                _ => string.Empty
            };
        }

        private bool IsStandardLibraryCall(IMethodSymbol symbol)
        {
            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            return _standardNamespaces.Any(s => ns.StartsWith(s));
        }

        private static bool IsMigrationClass(string className)
            => !string.IsNullOrEmpty(className) &&
               (className.Contains("Migration") || className.Contains("Migrations"));

        private static bool IsMigration(IMethodSymbol symbol)
        {
            var ct = symbol.ContainingType;
            if (ct != null)
            {
                if (ct.Name.Contains("Migration") || ct.Name.Contains("Migrations")) return true;
                var bt = ct.BaseType;
                while (bt != null)
                {
                    if (bt.Name == "Migration" ||
                        bt.ToDisplayString() == "Microsoft.EntityFrameworkCore.Migrations.Migration")
                        return true;
                    bt = bt.BaseType;
                }
            }
            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            return ns.Contains("Migration") || ns.Contains("Migrations");
        }
    }
}
