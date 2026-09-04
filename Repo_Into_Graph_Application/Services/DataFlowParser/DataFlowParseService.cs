using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Repo_Into_Graph_DataAccess.Models.Analysis;

namespace Repo_Into_Graph_Application.Services.DataFlowParser
{
    public class DataFlowParseService
    {
        public List<DataFlowEdge> ParseIntraMethodDataFlow(Guid analysisRunId, string className, string methodName, string sourceCode)
        {
            var edges = new List<DataFlowEdge>();
            if (string.IsNullOrWhiteSpace(sourceCode)) return edges;

            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            var assignments = root.DescendantNodes().OfType<AssignmentExpressionSyntax>();
            foreach (var assignment in assignments)
            {
                edges.Add(new DataFlowEdge
                {
                    Id = Guid.NewGuid(),
                    AnalysisRunId = analysisRunId,
                    ClassName = className,
                    MethodName = methodName,
                    SourceToken = assignment.Right.ToString(),
                    TargetToken = assignment.Left.ToString(),
                    RelationType = "Assignment",
                    CreatedAt = DateTime.UtcNow
                });
            }

            var localDeclarations = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();
            foreach (var declaration in localDeclarations)
            {
                foreach (var variable in declaration.Declaration.Variables)
                {
                    if (variable.Initializer != null)
                    {
                        edges.Add(new DataFlowEdge
                        {
                            Id = Guid.NewGuid(),
                            AnalysisRunId = analysisRunId,
                            ClassName = className,
                            MethodName = methodName,
                            SourceToken = variable.Initializer.Value.ToString(), 
                            TargetToken = variable.Identifier.ValueText,          
                            RelationType = "Assignment", 
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
          
            var returnStatements = root.DescendantNodes().OfType<ReturnStatementSyntax>();
            foreach (var ret in returnStatements)
            {
                if (ret.Expression != null)
                {
                    edges.Add(new DataFlowEdge
                    {
                        Id = Guid.NewGuid(),
                        AnalysisRunId = analysisRunId,
                        ClassName = className,
                        MethodName = methodName,
                        SourceToken = ret.Expression.ToString(),
                        TargetToken = "RETURN_VALUE",
                        RelationType = "Return",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            return edges;
        }

        public List<string> GetMethodParameters(string sourceCode)
        {
            if (string.IsNullOrWhiteSpace(sourceCode)) return new List<string>();

            string wrappedCode = $@"
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        
        public interface FakeInterfaceForParsing 
        {{
            {sourceCode}
        }}";

            SyntaxTree tree = CSharpSyntaxTree.ParseText(wrappedCode);

            var methodDeclaration = tree.GetCompilationUnitRoot()
                                        .DescendantNodes()
                                        .OfType<MethodDeclarationSyntax>()
                                        .FirstOrDefault();

            if (methodDeclaration == null) return new List<string>();

            return methodDeclaration.ParameterList.Parameters
                .Select(p => {
                    string typeStr = p.Type != null ? p.Type.ToString().Trim() : "unknown";
                    string nameStr = p.Identifier.ValueText.Trim();

                    string fullParam = $"{typeStr} {nameStr}";


                    return fullParam.Replace("<", "&lt;").Replace(">", "&gt;");
                })
                .ToList();
        }
    }
}





