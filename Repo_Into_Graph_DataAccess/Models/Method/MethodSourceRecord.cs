using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Models.Business;
using System;

namespace Repo_Into_Graph_DataAccess.Models.Method;

public class MethodSourceRecord
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Ngon ngu cua method, lay tu ILanguageParser.LanguageName luc phan tich repo
    /// (vd "C# (.NET)", "Java (Spring Boot)").
    ///
    /// Nullable vi cac ban ghi tao ra TRUOC migration AddLanguageToMethodSource deu
    /// khong co gia tri. Khi doc, LUON di qua LanguageNormalizer — no se suy luan tu
    /// SourceCode neu cot nay null, nen du lieu cu van dung duoc ma khong can phan
    /// tich lai toan bo repo.
    /// </summary>
    public string? Language { get; set; }

    public DateTime CreatedAt { get; set; }
    public Consts.NodeType Type { get; set; } = Consts.NodeType.Activity;
    public AnalysisRun? AnalysisRun { get; set; }
    public List<Repo_Into_Graph_DataAccess.Models.Feature.FeatureMethodMapping> FeatureMethodMappings { get; set; } = new();
}
