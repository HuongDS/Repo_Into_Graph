using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Repo_Into_Graph.Models;

namespace Repo_Into_Graph.Services;

public class DataFlowGraphExtractor
{
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

    public List<DataFlowNode> Extract(string filePath, SyntaxTree syntaxTree, SemanticModel semanticModel)
    {
        var root = (CompilationUnitSyntax)syntaxTree.GetRoot();
        var visitor = new DataFlowVisitor(semanticModel, filePath, _standardNamespaces);
        visitor.Visit(root);
        return visitor.DataFlowNodes;
    }

    private class DataFlowVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly string _filePath;
        private readonly HashSet<string> _standardNamespaces;
        private string _currentClass = string.Empty;
        private string _currentMethod = string.Empty;
        private readonly Dictionary<string, DataFlowNode> _variables = new();

        public List<DataFlowNode> DataFlowNodes { get; private set; } = new();

        public DataFlowVisitor(SemanticModel semanticModel, string filePath, HashSet<string> standardNamespaces)
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

            _variables.Clear();

            // Track parameters as data sources
            foreach (var parameter in node.ParameterList.Parameters)
            {
                var paramName = parameter.Identifier.Text;
                var paramType = parameter.Type?.ToString() ?? "Unknown";

                // Skip primitive types and built-in types
                if (!IsPrimitiveType(paramType))
                {
                    _variables[paramName] = new DataFlowNode
                    {
                        VariableName = paramName,
                        DataType = paramType,
                        SourceLocation = $"{_currentClass}.{_currentMethod}",
                        PassedThroughMethods = new()
                    };
                }
            }

            base.VisitMethodDeclaration(node);

            // Add tracked variables to result
            DataFlowNodes.AddRange(_variables.Values);

            _currentMethod = previousMethod;
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            var type = node.Type.ToString();

            // Skip primitive types and built-in types
            if (!IsPrimitiveType(type))
            {
                foreach (var variable in node.Variables)
                {
                    var varName = variable.Identifier.Text;
                    var location = $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

                    _variables[varName] = new DataFlowNode
                    {
                        VariableName = varName,
                        DataType = type,
                        SourceLocation = location,
                        PassedThroughMethods = new()
                    };
                }
            }

            base.VisitVariableDeclaration(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                // Track arguments passed to methods
                var arguments = node.ArgumentList.Arguments;
                for (int i = 0; i < arguments.Count; i++)
                {
                    var arg = arguments[i];
                    var argText = arg.Expression.ToString();

                    if (_variables.ContainsKey(argText))
                    {
                        var methodIdentifier = $"{methodSymbol.ContainingType?.Name ?? "Unknown"}.{methodSymbol.Name}";
                        _variables[argText].PassedThroughMethods.Add(methodIdentifier);
                    }
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            var returnValue = node.Expression?.ToString() ?? string.Empty;

            if (_variables.ContainsKey(returnValue))
            {
                _variables[returnValue].SinkLocation = $"{_currentClass}.{_currentMethod}";
                _variables[returnValue].SinkType = "return";
            }

            base.VisitReturnStatement(node);
        }

        private bool IsPrimitiveType(string typeName)
        {
            var primitiveTypes = new[] { "int", "string", "bool", "double", "float", "long", "short", "byte", "decimal", "char" };
            return primitiveTypes.Contains(typeName.ToLower()) || typeName.StartsWith("System.");
        }
    }
}
