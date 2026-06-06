using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Repo_Into_Graph.Models;

namespace Repo_Into_Graph.Services;

public class CallGraphExtractor
{
    private readonly List<CallGraphEdge> _callGraph = new();
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

    public ExtractionResult Extract(
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? interfaceImplementationMap = null)
    {
        var root = (CompilationUnitSyntax)syntaxTree.GetRoot();
        var visitor = new CallGraphVisitor(semanticModel, _standardNamespaces, interfaceImplementationMap);
        visitor.Visit(root);
        return new ExtractionResult
        {
            CallGraphEdges = visitor.CallGraphEdges,
            MethodSources = visitor.MethodSources
        };
    }

    private class CallGraphVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly HashSet<string> _standardNamespaces;
        private readonly IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? _interfaceImplementationMap;
        private string _currentClass = string.Empty;
        private string _currentMethod = string.Empty;
        private string _currentMethodDisplay = string.Empty;

        public List<CallGraphEdge> CallGraphEdges { get; } = new();
        public List<MethodSource> MethodSources { get; } = new();

        public CallGraphVisitor(
            SemanticModel semanticModel,
            HashSet<string> standardNamespaces,
            IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? interfaceImplementationMap)
        {
            _semanticModel = semanticModel;
            _standardNamespaces = standardNamespaces;
            _interfaceImplementationMap = interfaceImplementationMap;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var previousClass = _currentClass;
            _currentClass = node.Identifier.Text;
            base.VisitClassDeclaration(node);
            _currentClass = previousClass;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var previousMethod = _currentMethod;
            var previousMethodDisplay = _currentMethodDisplay;
            _currentMethod = node.Identifier.Text;
            _currentMethodDisplay = GetMethodDisplayName(node);

            if (!IsMigrationClass(_currentClass))
            {
                var sourceCode = node.ToString();
                MethodSources.Add(new MethodSource
                {
                    ClassName = _currentClass,
                    MethodName = _currentMethod,
                    SourceCode = sourceCode
                });

                if (ShouldIncludeMethodEntry(node))
                {
                    CallGraphEdges.Add(new CallGraphEdge
                    {
                        CallerClass = _currentClass,
                        CallerMethod = "__CLASS__",
                        CalleeClass = _currentClass,
                        CalleeMethod = string.IsNullOrEmpty(_currentMethodDisplay) ? node.Identifier.Text : _currentMethodDisplay
                    });
                }
            }

            base.VisitMethodDeclaration(node);
            _currentMethod = previousMethod;
            _currentMethodDisplay = previousMethodDisplay;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (_currentClass != string.Empty && _currentMethod != string.Empty)
            {
                if (!IsMigrationClass(_currentClass))
                {
                    var symbolInfo = _semanticModel.GetSymbolInfo(node);
                    var methodSymbol = ResolveMethodSymbol(symbolInfo);

                    if (methodSymbol != null)
                    {
                        var calleeClass = methodSymbol.ContainingType?.Name ?? "Unknown";
                        var calleeName = methodSymbol.Name;

                        // Filter out standard library calls and migration calls
                        if (!IsStandardLibraryCall(methodSymbol) && !IsMigrationClass(calleeClass) && !IsMigration(methodSymbol))
                        {
                            CallGraphEdges.Add(new CallGraphEdge
                            {
                                CallerClass = _currentClass,
                                CallerMethod = string.IsNullOrEmpty(_currentMethodDisplay) ? _currentMethod : _currentMethodDisplay,
                                CalleeClass = calleeClass,
                                CalleeMethod = calleeName
                            });

                            AddImplementationEdges(methodSymbol, node);
                        }
                    }
                }
            }

            base.VisitInvocationExpression(node);
        }

        private IMethodSymbol? ResolveMethodSymbol(SymbolInfo symbolInfo)
        {
            if (symbolInfo.Symbol is IMethodSymbol directSymbol)
            {
                return directSymbol;
            }

            if (symbolInfo.Symbol is IPropertySymbol propertySymbol && propertySymbol.GetMethod is IMethodSymbol getter)
            {
                return getter;
            }

            foreach (var candidate in symbolInfo.CandidateSymbols)
            {
                if (candidate is IMethodSymbol candidateMethod)
                {
                    return candidateMethod;
                }

                if (candidate is IPropertySymbol candidateProperty && candidateProperty.GetMethod is IMethodSymbol candidateGetter)
                {
                    return candidateGetter;
                }
            }

            return null;
        }

        private void AddImplementationEdges(IMethodSymbol interfaceMethod, InvocationExpressionSyntax node)
        {
            if (_interfaceImplementationMap == null)
            {
                return;
            }

            var containingType = interfaceMethod.ContainingType;
            if (containingType == null || containingType.TypeKind != TypeKind.Interface)
            {
                return;
            }

            if (!_interfaceImplementationMap.TryGetValue(interfaceMethod.OriginalDefinition, out var implementations))
            {
                return;
            }

            foreach (var implementation in implementations)
            {
                var calleeClass = implementation.ContainingType?.Name ?? "Unknown";
                if (!IsMigrationClass(containingType.Name) && !IsMigrationClass(calleeClass) && !IsMigration(implementation))
                {
                    CallGraphEdges.Add(new CallGraphEdge
                    {
                        CallerClass = containingType.Name,
                        CallerMethod = interfaceMethod.Name,
                        CalleeClass = calleeClass,
                        CalleeMethod = implementation.Name
                    });
                }
            }
        }

        private bool ShouldIncludeMethodEntry(MethodDeclarationSyntax node)
        {
            var hasHttpAttribute = node.AttributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .Any(attribute => IsHttpAttribute(attribute.Name.ToString()));

            var isControllerMethod = _currentClass.EndsWith("Controller", StringComparison.Ordinal) &&
                                     node.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword));

            return hasHttpAttribute || isControllerMethod;
        }

        private string GetMethodDisplayName(MethodDeclarationSyntax node)
        {
            var verb = node.AttributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .Select(attribute => GetHttpVerb(attribute.Name.ToString()))
                .FirstOrDefault(result => !string.IsNullOrEmpty(result));

            return string.IsNullOrEmpty(verb) ? node.Identifier.Text : $"{verb} {node.Identifier.Text}";
        }

        private static bool IsHttpAttribute(string attributeName)
        {
            if (attributeName.EndsWith("Attribute", StringComparison.Ordinal))
            {
                attributeName = attributeName[..^"Attribute".Length];
            }

            return attributeName is "HttpGet" or "Get"
                or "HttpPost" or "Post"
                or "HttpPut" or "Put"
                or "HttpDelete" or "Delete"
                or "HttpPatch" or "Patch";
        }

        private static string GetHttpVerb(string attributeName)
        {
            if (attributeName.EndsWith("Attribute", StringComparison.Ordinal))
            {
                attributeName = attributeName[..^"Attribute".Length];
            }

            return attributeName switch
            {
                "HttpGet" or "Get" => "GET",
                "HttpPost" or "Post" => "POST",
                "HttpPut" or "Put" => "PUT",
                "HttpDelete" or "Delete" => "DELETE",
                "HttpPatch" or "Patch" => "PATCH",
                _ => string.Empty
            };
        }

        private bool IsStandardLibraryCall(IMethodSymbol methodSymbol)
        {
            var containingNamespace = methodSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            return _standardNamespaces.Any(ns => containingNamespace.StartsWith(ns));
        }

        private bool IsMigrationClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return false;
            return className.Contains("Migration") || className.Contains("Migrations");
        }

        private bool IsMigration(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null) return false;
            
            var containingType = methodSymbol.ContainingType;
            if (containingType != null)
            {
                if (containingType.Name.Contains("Migration") || containingType.Name.Contains("Migrations"))
                {
                    return true;
                }
                
                var baseType = containingType.BaseType;
                while (baseType != null)
                {
                    if (baseType.Name == "Migration" || baseType.ToDisplayString() == "Microsoft.EntityFrameworkCore.Migrations.Migration")
                    {
                        return true;
                    }
                    baseType = baseType.BaseType;
                }
            }

            var containingNamespace = methodSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (containingNamespace.Contains("Migration") || containingNamespace.Contains("Migrations"))
            {
                return true;
            }

            return false;
        }
    }
}

public class ExtractionResult
{
    public List<CallGraphEdge> CallGraphEdges { get; set; } = new();
    public List<MethodSource> MethodSources { get; set; } = new();
}
