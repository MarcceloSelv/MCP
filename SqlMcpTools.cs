using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

/// <summary>
/// SQL MCP Server tools for T-SQL validation, parsing, documentation, and safe execution.
/// </summary>
[McpServerToolType]
public class SqlMcpTools
{
    private readonly SqlDocumentationService _documentationService;
    private readonly SqlExecutionService _executionService;
    private readonly SqlConnectionConfig _config;

    public SqlMcpTools(
        SqlConnectionConfig config,
        SqlDocumentationService documentationService,
        SqlExecutionService executionService)
    {
        _config = config;
        _documentationService = documentationService;
        _executionService = executionService;
    }

    [McpServerTool]
    [Description("Validates SQL Server T-SQL syntax and returns detailed error information if invalid. Supports all SQL Server versions from 2005 to 2022.")]
    public string ValidateSql(
        [Description("The SQL query to validate")] string query,
        [Description("SQL Server version: 90=2005, 100=2008, 110=2012, 120=2014, 130=2016, 140=2017, 150=2019, 160=2022 (default)")]
        string sqlVersion = "160")
    {
        var parser = GetParser(sqlVersion);
        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var errors);

        var versionName = GetVersionName(sqlVersion);

        var result = new
        {
            valid = errors.Count == 0,
            errorCount = errors.Count,
            sqlVersion,
            sqlVersionName = versionName,
            errors = errors.Select(e => new
            {
                line = e.Line,
                column = e.Column,
                message = e.Message,
                number = e.Number,
                offset = e.Offset
            }).ToArray(),
            summary = errors.Count == 0
                ? $"✓ SQL syntax is valid (validated against {versionName})"
                : $"✗ Found {errors.Count} syntax error(s) (validated against {versionName})"
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("Parses SQL and returns the abstract syntax tree (AST) structure with statement and table counts")]
    public string ParseSql(
        [Description("The SQL query to parse")] string query,
        [Description("SQL Server version (default: 160 for SQL Server 2022)")]
        string sqlVersion = "160")
    {
        var parser = GetParser(sqlVersion);
        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var errors);

        var result = new
        {
            valid = errors.Count == 0,
            sqlVersion,
            sqlVersionName = GetVersionName(sqlVersion),
            fragmentType = fragment?.GetType().Name,
            scriptTokenStream = fragment?.ScriptTokenStream?.Count ?? 0,
            errors = errors.Select(e => new
            {
                line = e.Line,
                column = e.Column,
                message = e.Message
            }).ToArray(),
            astInfo = fragment != null ? GetAstInfo(fragment) : "Parse failed"
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    //[McpServerTool]
    //[Description("Generates comprehensive Markdown documentation for SQL scripts including tables, functions, complexity analysis and recommendations")]
    //public string DocumentSql(
    //    [Description("The SQL script to document")] string query,
    //    [Description("SQL Server version (default: 160 for SQL Server 2022)")]
    //    string sqlVersion = "160")
    //{
    //    var result = _documentationService.GenerateDocumentation(query, sqlVersion);

    //    if (result.Success)
    //    {
    //        return result.MarkdownDocumentation!;
    //    }

    //    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    //}

    [McpServerTool]
    [Description("Executes SQL queries with automatic syntax validation and security checks. Blocked: DROP, DELETE, UPDATE, TRUNCATE, ALTER. Allowed: SELECT, INSERT, CREATE. ⚡ Validation is automatic - no need to call validate_sql first.")]
    public string ExecuteSql(
        [Description("The SQL query to execute")] string query,
        [Description("Database name to use (default: configured default database)")]
        string? database = null)
    {
        var result = _executionService.ExecuteQuery(query, database);
        return FormatExecutionResult(result);
    }

    [McpServerTool]
    [Description("Lists all configured databases and shows which one is the default")]
    public string ListDatabases()
    {
        var databases = _config.GetAvailableDatabases();
        var defaultDb = _config.DefaultDatabase;

        var result = new
        {
            success = true,
            defaultDatabase = defaultDb,
            availableDatabases = databases,
            summary = $"Default database: {defaultDb ?? "not set"}\nAvailable databases: {string.Join(", ", databases)}"
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
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

    private string GetVersionName(string version)
    {
        return version switch
        {
            "90" => "SQL Server 2005",
            "100" => "SQL Server 2008",
            "110" => "SQL Server 2012",
            "120" => "SQL Server 2014",
            "130" => "SQL Server 2016",
            "140" => "SQL Server 2017",
            "150" => "SQL Server 2019",
            "160" => "SQL Server 2022",
            _ => "SQL Server 2022"
        };
    }

    private string GetAstInfo(TSqlFragment fragment)
    {
        var visitor = new SqlFragmentVisitor();
        fragment.Accept(visitor);
        return $"Statements: {visitor.StatementCount}, Tables: {visitor.TableCount}";
    }

    private string FormatExecutionResult(SqlExecutionResult result)
    {
        if (!result.Success)
        {
            var errorJson = new
            {
                success = false,
                errorMessage = result.ErrorMessage,
                blockedCommands = result.BlockedCommands.Count > 0 ? result.BlockedCommands : null,
                sqlErrorNumber = result.SqlErrorNumber,
                sqlErrorLineNumber = result.SqlErrorLineNumber
            };
            return JsonSerializer.Serialize(errorJson, new JsonSerializerOptions { WriteIndented = true });
        }

        var output = new System.Text.StringBuilder();
        output.AppendLine($"✓ Query executed successfully on database: {result.DatabaseUsed}");
        output.AppendLine($"Rows returned: {result.RowsAffected}");

        if (result.WasAutoFixed && !string.IsNullOrEmpty(result.OriginalQuery))
        {
            output.AppendLine();
            output.AppendLine("🔧 AUTO-FIX APPLIED:");
            output.AppendLine("   Reserved keywords were automatically enclosed in square brackets.");
            output.AppendLine($"   Original: {result.OriginalQuery.Substring(0, Math.Min(150, result.OriginalQuery.Length))}...");
        }

        if (result.InfoMessages.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("--- Informational Messages ---");
            foreach (var message in result.InfoMessages)
            {
                output.AppendLine(message);
            }
        }

        output.AppendLine();

        if (result.ResultTables.Count == 0)
        {
            output.AppendLine("No result sets returned.");
        }
        else
        {
            for (int tableIndex = 0; tableIndex < result.ResultTables.Count; tableIndex++)
            {
                var table = result.ResultTables[tableIndex];

                if (result.ResultTables.Count > 1)
                {
                    output.AppendLine($"--- Result Set {tableIndex + 1} ---");
                }

                if (table.Columns.Count > 0)
                {
                    output.AppendLine("| " + string.Join(" | ", table.Columns) + " |");
                    output.AppendLine("| " + string.Join(" | ", table.Columns.Select(_ => "---")) + " |");

                    foreach (var row in table.Rows)
                    {
                        var formattedRow = row.Select(cell => cell?.ToString() ?? "NULL");
                        output.AppendLine("| " + string.Join(" | ", formattedRow) + " |");
                    }
                }

                output.AppendLine();
            }
        }

        return output.ToString();
    }
}

public class SqlFragmentVisitor : TSqlFragmentVisitor
{
    public int StatementCount { get; private set; }
    public int TableCount { get; private set; }

    public override void Visit(TSqlStatement node)
    {
        StatementCount++;
        base.Visit(node);
    }

    public override void Visit(NamedTableReference node)
    {
        TableCount++;
        base.Visit(node);
    }
}
