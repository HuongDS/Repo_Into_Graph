using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Repo_Into_Graph.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Services.Parsers;

public class CSharpParser : ILanguageParser
{
    public string LanguageName => "C# (.NET)";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".cs" };

    private readonly HashSet<string> _standardNamespaces = new()
    {
        "System", "System.Collections", "System.Collections.Generic", "System.Linq",
        "System.Text", "System.IO", "System.Threading", "System.Diagnostics",
        "System.Net", "System.Reflection"
    };

    // Hàm phân tích tập trung nhận vào danh sách tất cả các cây syntax trong Solution
    public ExtractionResult ParseWithFullContext(SyntaxTree targetTree, List<SyntaxTree> allTrees, string filePath)
    {
        var result = new ExtractionResult();

        try
        {
            // Tạo một Compilation chung chứa TOÀN BỘ file của project để không bị mất Context
            var compilation = CSharpCompilation.Create("FullRepoAnalysis")
                .AddSyntaxTrees(allTrees)
                .AddReferences(GetReferenceAssemblies());

            var semanticModel = compilation.GetSemanticModel(targetTree);
            var interfaceImplementationMap = BuildInterfaceImplementationMap(compilation);

            var root = (CompilationUnitSyntax)targetTree.GetRoot();
            var visitor = new CallGraphVisitor(semanticModel, _standardNamespaces, interfaceImplementationMap, LanguageName);
            visitor.Visit(root);

            result.CallGraphEdges = visitor.CallGraphEdges;
            result.MethodSources = visitor.MethodSources;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  C# parse error in {Path.GetFileName(filePath)}: {ex.Message}");
        }

        return result;
    }

    // Giữ nguyên interface cũ để không lỗi compile, nhưng sẽ xử lý đơn lẻ (hoặc không dùng tới nếu gọi trực tiếp)
    public Task<ExtractionResult> ParseAsync(string filePath, string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var res = ParseWithFullContext(syntaxTree, new List<SyntaxTree> { syntaxTree }, filePath);
        return Task.FromResult(res);
    }

    private static List<MetadataReference> GetReferenceAssemblies()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;
        var coreLibPath = Path.Combine(runtimePath, "System.Runtime.dll");
        if (File.Exists(coreLibPath))
            references.Add(MetadataReference.CreateFromFile(coreLibPath));

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

    private class CallGraphVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly HashSet<string> _standardNamespaces;
        private readonly IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? _interfaceImplementationMap;
        private readonly string _language;
        private string _currentClass = string.Empty;
        private string _currentMethod = string.Empty;

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

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            //update
            var prevMethod = _currentMethod;
            _currentMethod = node.Identifier.Text;
            var parentType = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

            string actualClassName = parentType != null ? parentType.Identifier.Text.Trim() : _currentClass;
            if (!IsMigrationClass(actualClassName))
            {
                MethodSources.Add(new MethodSource
                {
                    ClassName = actualClassName,
                    MethodName = _currentMethod,
                    SourceCode = node.ToString(),
                    Language = _language
                });
            }

            base.VisitMethodDeclaration(node);
            _currentMethod = prevMethod;
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
                            CallerMethod = _currentMethod, // Dùng tên thuần túy đồng bộ
                            CalleeClass = calleeClass,
                            CalleeMethod = calleeName,
                            Language = _language
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
                        Language = _language
                    });
                }
            }
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