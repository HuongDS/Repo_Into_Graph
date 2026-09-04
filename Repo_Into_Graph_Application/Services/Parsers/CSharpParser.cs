using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Method;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Parsers;

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
            var mediatorHandlerMap = BuildMediatorHandlerMap(allTrees);

            var root = (CompilationUnitSyntax)targetTree.GetRoot();
            var visitor = new CallGraphVisitor(semanticModel, _standardNamespaces, interfaceImplementationMap, mediatorHandlerMap, LanguageName);
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

    // CQRS/MediatR dispatch (vd: _mediator.Send(new CreateOrderCommand(...))) không tạo ra một lời gọi
    // hàm thật sự tới Handler - việc "nối" Command/Query với Handler xử lý nó là do DI resolve theo kiểu
    // generic lúc runtime, Roslyn không thấy được cạnh gọi hàm này. Ngoài ra vì repo được clone về chỉ
    // parse bằng syntax tree thô (không restore NuGet), assembly MediatR không được reference nên
    // IRequestHandler<,>/INotificationHandler<> không resolve được bằng semantic model.
    // => Quét theo cú pháp (không cần semantic) để lập bản đồ: tên kiểu Command/Query/Notification
    // -> (Class Handler, tên method xử lý), dùng để tự tạo cạnh ảo Caller -> Handler khi gặp Send/Publish.
    private static Dictionary<string, List<(string ClassName, string MethodName)>> BuildMediatorHandlerMap(List<SyntaxTree> allTrees)
    {
        var map = new Dictionary<string, List<(string ClassName, string MethodName)>>();

        foreach (var tree in allTrees)
        {
            foreach (var classDecl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (classDecl.BaseList == null) continue;

                foreach (var baseType in classDecl.BaseList.Types)
                {
                    if (baseType.Type is not GenericNameSyntax generic) continue;
                    if (generic.Identifier.Text != "IRequestHandler" && generic.Identifier.Text != "INotificationHandler")
                        continue;
                    if (generic.TypeArgumentList.Arguments.Count == 0) continue;

                    var requestType = GetSimpleTypeName(generic.TypeArgumentList.Arguments[0]);
                    if (string.IsNullOrEmpty(requestType)) continue;

                    // Theo convention MediatR, method xử lý luôn tên "Handle" (kể cả khi implement tường minh).
                    var handlerMethod = classDecl.Members
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(m => m.Identifier.Text == "Handle")?.Identifier.Text ?? "Handle";

                    var key = requestType.Trim().ToLowerInvariant();
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = new List<(string, string)>();
                        map[key] = list;
                    }

                    if (!list.Any(h => h.ClassName == classDecl.Identifier.Text && h.MethodName == handlerMethod))
                        list.Add((classDecl.Identifier.Text, handlerMethod));
                }
            }
        }

        return map;
    }

    private static string GetSimpleTypeName(TypeSyntax type) => type switch
    {
        GenericNameSyntax g => g.Identifier.Text,
        QualifiedNameSyntax q => GetSimpleTypeName(q.Right),
        AliasQualifiedNameSyntax a => GetSimpleTypeName(a.Name),
        SimpleNameSyntax s => s.Identifier.Text,
        _ => type.ToString()
    };

    private class CallGraphVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly HashSet<string> _standardNamespaces;
        private readonly IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? _interfaceImplementationMap;
        private readonly IReadOnlyDictionary<string, List<(string ClassName, string MethodName)>>? _mediatorHandlerMap;
        private readonly string _language;
        private string _currentClass = string.Empty;
        private string _currentMethod = string.Empty;
        private Stack<string> _conditionStack = new();

        public List<CallGraphEdge> CallGraphEdges { get; } = new();
        public List<MethodSource> MethodSources { get; } = new();

        public CallGraphVisitor(
            SemanticModel semanticModel,
            HashSet<string> standardNamespaces,
            IReadOnlyDictionary<IMethodSymbol, List<IMethodSymbol>>? interfaceImplementationMap,
            IReadOnlyDictionary<string, List<(string ClassName, string MethodName)>>? mediatorHandlerMap,
            string language)
        {
            _semanticModel = semanticModel;
            _standardNamespaces = standardNamespaces;
            _interfaceImplementationMap = interfaceImplementationMap;
            _mediatorHandlerMap = mediatorHandlerMap;
            _language = language;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var prev = _currentClass;
            _currentClass = node.Identifier.Text;
            base.VisitClassDeclaration(node);
            _currentClass = prev;
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            // Visit condition first to capture any method calls inside it
            Visit(node.Condition);

            var conditionText = node.Condition.ToString();
            // Sanitize and trim if too long
            if (conditionText.Length > 80) conditionText = conditionText.Substring(0, 77) + "...";
            conditionText = conditionText.Replace("\"", "'").Replace("\n", " ").Replace("\r", "");

            _conditionStack.Push(conditionText);
            Visit(node.Statement);
            _conditionStack.Pop();

            if (node.Else != null)
            {
                _conditionStack.Push($"!({conditionText})");
                Visit(node.Else);
                _conditionStack.Pop();
            }
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
                bool isDecisionGateway = node.DescendantNodes().Any(n => 
                    n is IfStatementSyntax || 
                    n is SwitchStatementSyntax || 
                    n is SwitchExpressionSyntax || 
                    n is ConditionalExpressionSyntax ||
                    n is WhileStatementSyntax ||
                    n is DoStatementSyntax ||
                    n is ForStatementSyntax ||
                    n is ForEachStatementSyntax);

                MethodSources.Add(new MethodSource
                {
                    ClassName = actualClassName,
                    MethodName = _currentMethod,
                    SourceCode = node.ToString(),
                    Language = _language,
                    Type = isDecisionGateway ? Repo_Into_Graph_DataAccess.Consts.NodeType.DecisionGateway : Repo_Into_Graph_DataAccess.Consts.NodeType.Activity
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
                            Language = _language,
                            ConditionContext = _conditionStack.Count > 0 ? _conditionStack.Peek() : null
                        });

                        AddImplementationEdges(methodSymbol);
                    }
                }
                else
                {
                    // methodSymbol null: thường do gọi vào 1 assembly ngoài không được reference
                    // (vd MediatR). Thử suy ra cạnh Caller -> Handler thật qua map cú pháp bên trên.
                    TryEmitMediatorDispatchEdge(node);
                }
            }

            base.VisitInvocationExpression(node);
        }

        private void TryEmitMediatorDispatchEdge(InvocationExpressionSyntax node)
        {
            if (_mediatorHandlerMap == null || _mediatorHandlerMap.Count == 0) return;
            if (node.Expression is not MemberAccessExpressionSyntax memberAccess) return;

            var calledMember = memberAccess.Name.Identifier.Text;
            if (calledMember != "Send" && calledMember != "Publish") return;

            var argExpr = node.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (argExpr == null) return;

            // Ưu tiên suy ra kiểu tham số qua semantic model (hoạt động tốt cho các biến/command khai
            // báo trong chính repo, vì các kiểu đó đã có trong compilation dù MediatR thì không).
            string? requestTypeName = _semanticModel.GetTypeInfo(argExpr).Type?.Name;
            if (string.IsNullOrEmpty(requestTypeName) && argExpr is ObjectCreationExpressionSyntax creation)
                requestTypeName = GetSimpleTypeName(creation.Type);

            if (string.IsNullOrEmpty(requestTypeName)) return;

            var key = requestTypeName.Trim().ToLowerInvariant();
            if (!_mediatorHandlerMap.TryGetValue(key, out var handlers)) return;

            foreach (var (handlerClass, handlerMethod) in handlers)
            {
                if (IsMigrationClass(handlerClass)) continue;
                if (handlerClass == _currentClass && handlerMethod == _currentMethod) continue;

                CallGraphEdges.Add(new CallGraphEdge
                {
                    CallerClass = _currentClass,
                    CallerMethod = _currentMethod,
                    CalleeClass = handlerClass,
                    CalleeMethod = handlerMethod,
                    Language = _language,
                    ConditionContext = _conditionStack.Count > 0 ? _conditionStack.Peek() : null
                });
            }
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
                        ConditionContext = _conditionStack.Count > 0 ? _conditionStack.Peek() : null
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




