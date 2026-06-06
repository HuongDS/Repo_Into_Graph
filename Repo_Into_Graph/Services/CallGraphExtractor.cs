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

    public List<CallGraphEdge> Extract(
        string filePath,
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? interfaceImplementationMap = null)
    {
        var root = (CompilationUnitSyntax)syntaxTree.GetRoot();
        var visitor = new CallGraphVisitor(semanticModel, filePath, _standardNamespaces, interfaceImplementationMap);
        visitor.Visit(root);
        return visitor.CallGraphEdges;
    }

    private class CallGraphVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly string _filePath;
        private readonly HashSet<string> _standardNamespaces;
        private readonly IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? _interfaceImplementationMap;
        private string _currentClass = string.Empty;
        private string _currentMethod = string.Empty;
        private string _currentMethodDisplay = string.Empty;

        public List<CallGraphEdge> CallGraphEdges { get; } = new();

        public CallGraphVisitor(
            SemanticModel semanticModel,
            string filePath,
            HashSet<string> standardNamespaces,
            IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? interfaceImplementationMap)
        {
            _semanticModel = semanticModel;
            _filePath = filePath;
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

            if (ShouldIncludeMethodEntry(node))
            {
                CallGraphEdges.Add(new CallGraphEdge
                {
                    CallerClass = _currentClass,
                    CallerMethod = "__CLASS__",
                    CalleeClass = _currentClass,
                    CalleeMethod = string.IsNullOrEmpty(_currentMethodDisplay) ? node.Identifier.Text : _currentMethodDisplay,
                    FilePath = _filePath,
                    LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                });
            }

            base.VisitMethodDeclaration(node);
            _currentMethod = previousMethod;
            _currentMethodDisplay = previousMethodDisplay;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (_currentClass != string.Empty && _currentMethod != string.Empty)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(node);
                var methodSymbol = ResolveMethodSymbol(symbolInfo);

                if (methodSymbol != null)
                {
                    var calleeClass = methodSymbol.ContainingType?.Name ?? "Unknown";
                    var calleeName = methodSymbol.Name;

                    // Filter out standard library calls
                    if (!IsStandardLibraryCall(methodSymbol))
                    {
                        CallGraphEdges.Add(new CallGraphEdge
                        {
                            CallerClass = _currentClass,
                            CallerMethod = string.IsNullOrEmpty(_currentMethodDisplay) ? _currentMethod : _currentMethodDisplay,
                            CalleeClass = calleeClass,
                            CalleeMethod = calleeName,
                            FilePath = _filePath,
                            LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                        });

                        AddImplementationEdges(methodSymbol, node);
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
                CallGraphEdges.Add(new CallGraphEdge
                {
                    CallerClass = containingType.Name,
                    CallerMethod = interfaceMethod.Name,
                    CalleeClass = implementation.ContainingType?.Name ?? "Unknown",
                    CalleeMethod = implementation.Name,
                    FilePath = _filePath,
                    LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                });
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
    }
}
