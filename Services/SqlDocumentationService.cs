using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text;

public class SqlDocumentationService
{
    public DocumentationResult GenerateDocumentation(string sqlScript, string sqlVersion = "160")
    {
        try
        {
            var parser = GetParser(sqlVersion);
            
            using var reader = new StringReader(sqlScript);
            var fragment = parser.Parse(reader, out var errors);

            if (errors.Count > 0)
            {
                return new DocumentationResult
                {
                    Success = false,
                    Errors = errors.Select(e => $"Line {e.Line}, Column {e.Column}: {e.Message}").ToList()
                };
            }

            var analyzer = new SqlDocumentationAnalyzer();
            fragment.Accept(analyzer);

            var markdown = GenerateMarkdown(analyzer, sqlScript);

            return new DocumentationResult
            {
                Success = true,
                MarkdownDocumentation = markdown,
                Summary = analyzer.GetSummary()
            };
        }
        catch (Exception ex)
        {
            return new DocumentationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private string GenerateMarkdown(SqlDocumentationAnalyzer analyzer, string sqlScript)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# SQL Script Documentation");
        sb.AppendLine();

        // Summary section
        sb.AppendLine("## 📊 Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Statements:** {analyzer.TotalStatements}");
        sb.AppendLine($"- **Tables Referenced:** {analyzer.Tables.Count}");
        sb.AppendLine($"- **Functions Used:** {analyzer.Functions.Count}");
        sb.AppendLine($"- **Stored Procedures:** {analyzer.Procedures.Count}");
        sb.AppendLine($"- **Complexity Score:** {CalculateComplexity(analyzer)}/10");
        sb.AppendLine();

        // Statement breakdown
        sb.AppendLine("## 📝 Statement Breakdown");
        sb.AppendLine();
        sb.AppendLine($"- **SELECT Statements:** {analyzer.SelectCount}");
        sb.AppendLine($"- **INSERT Statements:** {analyzer.InsertCount}");
        sb.AppendLine($"- **UPDATE Statements:** {analyzer.UpdateCount}");
        sb.AppendLine($"- **DELETE Statements:** {analyzer.DeleteCount}");
        sb.AppendLine($"- **CREATE Statements:** {analyzer.CreateCount}");
        sb.AppendLine($"- **ALTER Statements:** {analyzer.AlterCount}");
        sb.AppendLine($"- **DROP Statements:** {analyzer.DropCount}");
        sb.AppendLine();

        // Tables section
        if (analyzer.Tables.Any())
        {
            sb.AppendLine("## 🗂️ Tables Referenced");
            sb.AppendLine();
            foreach (var table in analyzer.Tables.OrderBy(t => t))
            {
                sb.AppendLine($"- `{table}`");
            }
            sb.AppendLine();
        }

        // Functions section
        if (analyzer.Functions.Any())
        {
            sb.AppendLine("## ⚙️ Functions Used");
            sb.AppendLine();
            foreach (var function in analyzer.Functions.OrderBy(f => f))
            {
                sb.AppendLine($"- `{function}`");
            }
            sb.AppendLine();
        }

        // Procedures section
        if (analyzer.Procedures.Any())
        {
            sb.AppendLine("## 📦 Stored Procedures");
            sb.AppendLine();
            foreach (var proc in analyzer.Procedures)
            {
                sb.AppendLine($"### `{proc.Name}`");
                sb.AppendLine();
                
                if (proc.Parameters.Any())
                {
                    sb.AppendLine("**Parameters:**");
                    foreach (var param in proc.Parameters)
                    {
                        sb.AppendLine($"- `{param.Name}` ({param.DataType})");
                    }
                    sb.AppendLine();
                }

                if (proc.TablesUsed.Any())
                {
                    sb.AppendLine("**Tables Used:**");
                    foreach (var table in proc.TablesUsed)
                    {
                        sb.AppendLine($"- `{table}`");
                    }
                    sb.AppendLine();
                }
            }
        }

        // Joins analysis
        if (analyzer.JoinCount > 0)
        {
            sb.AppendLine("## 🔗 Join Analysis");
            sb.AppendLine();
            sb.AppendLine($"- **Total Joins:** {analyzer.JoinCount}");
            sb.AppendLine($"- **INNER JOINs:** {analyzer.InnerJoinCount}");
            sb.AppendLine($"- **LEFT JOINs:** {analyzer.LeftJoinCount}");
            sb.AppendLine($"- **RIGHT JOINs:** {analyzer.RightJoinCount}");
            sb.AppendLine($"- **FULL JOINs:** {analyzer.FullJoinCount}");
            sb.AppendLine($"- **CROSS JOINs:** {analyzer.CrossJoinCount}");
            sb.AppendLine();
        }

        // Complexity analysis
        sb.AppendLine("## 📈 Complexity Analysis");
        sb.AppendLine();
        sb.AppendLine($"- **Subqueries:** {analyzer.SubqueryCount}");
        sb.AppendLine($"- **CTEs (WITH clauses):** {analyzer.CteCount}");
        sb.AppendLine($"- **CASE statements:** {analyzer.CaseCount}");
        sb.AppendLine($"- **Transactions:** {analyzer.TransactionCount}");
        sb.AppendLine();

        // Recommendations
        var recommendations = GenerateRecommendations(analyzer);
        if (recommendations.Any())
        {
            sb.AppendLine("## 💡 Recommendations");
            sb.AppendLine();
            foreach (var rec in recommendations)
            {
                sb.AppendLine($"- {rec}");
            }
            sb.AppendLine();
        }

        // Original SQL
        sb.AppendLine("## 📄 Original SQL");
        sb.AppendLine();
        sb.AppendLine("```sql");
        sb.AppendLine(sqlScript);
        sb.AppendLine("```");

        return sb.ToString();
    }

    private int CalculateComplexity(SqlDocumentationAnalyzer analyzer)
    {
        int complexity = 0;
        
        complexity += analyzer.SelectCount;
        complexity += analyzer.InsertCount * 2;
        complexity += analyzer.UpdateCount * 2;
        complexity += analyzer.DeleteCount * 2;
        complexity += analyzer.JoinCount * 2;
        complexity += analyzer.SubqueryCount * 3;
        complexity += analyzer.CteCount * 2;
        complexity += analyzer.CaseCount;
        
        // Normalize to 0-10 scale
        return Math.Min(10, Math.Max(1, complexity / 3));
    }

    private List<string> GenerateRecommendations(SqlDocumentationAnalyzer analyzer)
    {
        var recommendations = new List<string>();

        if (analyzer.SelectCount > 5)
            recommendations.Add("Consider consolidating multiple SELECT statements if possible");

        if (analyzer.SubqueryCount > 3)
            recommendations.Add("High number of subqueries detected - consider using CTEs for better readability");

        if (analyzer.JoinCount > 5)
            recommendations.Add("Complex JOIN structure - ensure proper indexing on join columns");

        if (analyzer.DeleteCount > 0 && analyzer.TransactionCount == 0)
            recommendations.Add("DELETE statements without explicit transaction - consider wrapping in BEGIN TRANSACTION");

        if (analyzer.CrossJoinCount > 0)
            recommendations.Add("CROSS JOIN detected - ensure this is intentional as it creates cartesian products");

        if (analyzer.TransactionCount > analyzer.DeleteCount + analyzer.UpdateCount + analyzer.InsertCount)
            recommendations.Add("More transactions than DML statements - review transaction boundaries");

        return recommendations;
    }

    private TSqlParser GetParser(string sqlVersion)
    {
        return sqlVersion switch
        {
            "90" => new TSql90Parser(true),
            "100" => new TSql100Parser(true),
            "110" => new TSql110Parser(true),
            "120" => new TSql120Parser(true),
            "130" => new TSql130Parser(true),
            "140" => new TSql140Parser(true),
            "150" => new TSql150Parser(true),
            "160" => new TSql160Parser(true),
            _ => new TSql160Parser(true)
        };
    }
}

public class SqlDocumentationAnalyzer : TSqlFragmentVisitor
{
    public int TotalStatements { get; private set; }
    public int SelectCount { get; private set; }
    public int InsertCount { get; private set; }
    public int UpdateCount { get; private set; }
    public int DeleteCount { get; private set; }
    public int CreateCount { get; private set; }
    public int AlterCount { get; private set; }
    public int DropCount { get; private set; }
    public int JoinCount { get; private set; }
    public int InnerJoinCount { get; private set; }
    public int LeftJoinCount { get; private set; }
    public int RightJoinCount { get; private set; }
    public int FullJoinCount { get; private set; }
    public int CrossJoinCount { get; private set; }
    public int SubqueryCount { get; private set; }
    public int CteCount { get; private set; }
    public int CaseCount { get; private set; }
    public int TransactionCount { get; private set; }
    
    public HashSet<string> Tables { get; } = new();
    public HashSet<string> Functions { get; } = new();
    public List<ProcedureInfo> Procedures { get; } = new();

    private ProcedureInfo? _currentProcedure;

    public override void Visit(TSqlStatement node)
    {
        TotalStatements++;
        base.Visit(node);
    }

    public override void Visit(SelectStatement node)
    {
        SelectCount++;
        base.Visit(node);
    }

    public override void Visit(InsertStatement node)
    {
        InsertCount++;
        base.Visit(node);
    }

    public override void Visit(UpdateStatement node)
    {
        UpdateCount++;
        base.Visit(node);
    }

    public override void Visit(DeleteStatement node)
    {
        DeleteCount++;
        base.Visit(node);
    }

    public override void Visit(CreateProcedureStatement node)
    {
        CreateCount++;
        
        var procInfo = new ProcedureInfo
        {
            Name = node.ProcedureReference.Name.BaseIdentifier.Value
        };

        foreach (var param in node.Parameters)
        {
            procInfo.Parameters.Add(new ParameterInfo
            {
                Name = param.VariableName.Value,
                DataType = param.DataType?.Name?.BaseIdentifier?.Value ?? "unknown"
            });
        }

        _currentProcedure = procInfo;
        Procedures.Add(procInfo);
        
        base.Visit(node);
        
        _currentProcedure = null;
    }

    public override void Visit(AlterTableAddTableElementStatement node)
    {
        AlterCount++;
        base.Visit(node);
    }

    public override void Visit(DropTableStatement node)
    {
        DropCount++;
        base.Visit(node);
    }

    public override void Visit(NamedTableReference node)
    {
        var tableName = GetFullTableName(node.SchemaObject);
        Tables.Add(tableName);
        
        if (_currentProcedure != null)
        {
            _currentProcedure.TablesUsed.Add(tableName);
        }
        
        base.Visit(node);
    }

    public override void Visit(FunctionCall node)
    {
        // FunctionName is an Identifier, so we just use its Value
        var functionName = node.FunctionName.Value;
        Functions.Add(functionName);
        base.Visit(node);
    }

    public override void Visit(QualifiedJoin node)
    {
        JoinCount++;
        
        switch (node.QualifiedJoinType)
        {
            case QualifiedJoinType.Inner:
                InnerJoinCount++;
                break;
            case QualifiedJoinType.LeftOuter:
                LeftJoinCount++;
                break;
            case QualifiedJoinType.RightOuter:
                RightJoinCount++;
                break;
            case QualifiedJoinType.FullOuter:
                FullJoinCount++;
                break;
        }
        
        base.Visit(node);
    }

    public override void Visit(UnqualifiedJoin node)
    {
        CrossJoinCount++;
        base.Visit(node);
    }

    public override void Visit(ScalarSubquery node)
    {
        SubqueryCount++;
        base.Visit(node);
    }

    public override void Visit(CommonTableExpression node)
    {
        CteCount++;
        base.Visit(node);
    }

    public override void Visit(SearchedCaseExpression node)
    {
        CaseCount++;
        base.Visit(node);
    }

    public override void Visit(BeginTransactionStatement node)
    {
        TransactionCount++;
        base.Visit(node);
    }

    private string GetFullTableName(SchemaObjectName schemaObject)
    {
        var parts = new List<string>();
        
        if (schemaObject.DatabaseIdentifier != null)
            parts.Add(schemaObject.DatabaseIdentifier.Value);
        
        if (schemaObject.SchemaIdentifier != null)
            parts.Add(schemaObject.SchemaIdentifier.Value);
        
        parts.Add(schemaObject.BaseIdentifier.Value);
        
        return string.Join(".", parts);
    }

    private string GetFullFunctionName(SchemaObjectName functionName)
    {
        return GetFullTableName(functionName);
    }

    public string GetSummary()
    {
        return $"{TotalStatements} statements, {Tables.Count} tables, {JoinCount} joins, complexity {CalculateSimpleComplexity()}/10";
    }

    private int CalculateSimpleComplexity()
    {
        int complexity = SelectCount + (InsertCount + UpdateCount + DeleteCount) * 2 + 
                        JoinCount * 2 + SubqueryCount * 3;
        return Math.Min(10, Math.Max(1, complexity / 3));
    }
}

public class ProcedureInfo
{
    public string Name { get; set; } = "";
    public List<ParameterInfo> Parameters { get; set; } = new();
    public HashSet<string> TablesUsed { get; set; } = new();
}

public class ParameterInfo
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
}

public class DocumentationResult
{
    public bool Success { get; set; }
    public string? MarkdownDocumentation { get; set; }
    public string? Summary { get; set; }
    public List<string>? Errors { get; set; }
    public string? ErrorMessage { get; set; }
}
