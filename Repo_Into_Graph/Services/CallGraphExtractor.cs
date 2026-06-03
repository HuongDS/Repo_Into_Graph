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

    public List<CallGraphEdge> Extract(string filePath, SyntaxTree syntaxTree, SemanticModel semanticModel)
    {
        var root = (CompilationUnitSyntax)syntaxTree.GetRoot();
        var visitor = new CallGraphVisitor(semanticModel, filePath, _standardNamespaces);
        visitor.Visit(root);
        return visitor.CallGraphEdges;
    }

    private class CallGraphVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly string _filePath;
        private readonly HashSet<string> _standardNamespaces;
        private string _currentClass = string.Empty;
        private string _currentMethod = string.Empty;

        public List<CallGraphEdge> CallGraphEdges { get; } = new();

        public CallGraphVisitor(SemanticModel semanticModel, string filePath, HashSet<string> standardNamespaces)
        {
            _semanticModel = semanticModel;
            _filePath = filePath;
            _standardNamespaces = standardNamespaces;
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
            _currentMethod = node.Identifier.Text;
            base.VisitMethodDeclaration(node);
            _currentMethod = previousMethod;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (_currentClass != string.Empty && _currentMethod != string.Empty)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(node);
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                {
                    var calleeClass = methodSymbol.ContainingType?.Name ?? "Unknown";
                    var calleeName = methodSymbol.Name;

                    // Filter out standard library calls
                    if (!IsStandardLibraryCall(methodSymbol))
                    {
                        CallGraphEdges.Add(new CallGraphEdge
                        {
                            CallerClass = _currentClass,
                            CallerMethod = _currentMethod,
                            CalleeClass = calleeClass,
                            CalleeMethod = calleeName,
                            FilePath = _filePath,
                            LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                        });
                    }
                }
            }

            base.VisitInvocationExpression(node);
        }

        private bool IsStandardLibraryCall(IMethodSymbol methodSymbol)
        {
            var containingNamespace = methodSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            return _standardNamespaces.Any(ns => containingNamespace.StartsWith(ns));
        }
    }
}
