using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text.Json;
using System.Text.Json.Nodes;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new SqlValidatorMcpServer();
        await server.RunAsync();
    }
}

public class SqlValidatorMcpServer
{
    public async Task RunAsync()
    {
        await Console.Error.WriteLineAsync("SQL Validator MCP Server starting...");
        
        while (true)
        {
            var line = await Console.In.ReadLineAsync();
            if (line == null) break;
            
            try
            {
                var request = JsonSerializer.Deserialize<JsonNode>(line);
                var response = HandleRequest(request);
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                var error = new
                {
                    jsonrpc = "2.0",
                    id = (string?)null,
                    error = new { code = -32603, message = ex.Message }
                };
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(error));
            }
        }
    }

    private object HandleRequest(JsonNode? request)
    {
        if (request == null) throw new Exception("Invalid request");
        
        var method = request["method"]?.GetValue<string>();
        var id = request["id"]?.GetValue<string>();

        return method switch
        {
            "initialize" => HandleInitialize(id),
            "tools/list" => HandleToolsList(id),
            "tools/call" => HandleToolCall(request, id),
            _ => new
            {
                jsonrpc = "2.0",
                id,
                error = new { code = -32601, message = $"Method not found: {method}" }
            }
        };
    }

    private object HandleInitialize(string? id)
    {
        return new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new
                {
                    name = "sql-validator-mcp",
                    version = "1.0.0"
                },
                capabilities = new
                {
                    tools = new { }
                }
            }
        };
    }

    private object HandleToolsList(string? id)
    {
        return new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                tools = new[]
                {
                    new
                    {
                        name = "validate_sql",
                        description = "Validates SQL Server T-SQL syntax and returns detailed error information if invalid. Supports all SQL Server versions from 2005 to 2022.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new
                                {
                                    type = "string",
                                    description = "The SQL query to validate"
                                },
                                sqlVersion = new
                                {
                                    type = "string",
                                    description = "SQL Server version: 90=2005, 100=2008, 110=2012, 120=2014, 130=2016, 140=2017, 150=2019, 160=2022 (default)",
                                    @enum = new[] { "90", "100", "110", "120", "130", "140", "150", "160" },
                                    @default = "160"
                                }
                            },
                            required = new[] { "query" }
                        }
                    },
                    new
                    {
                        name = "parse_sql",
                        description = "Parses SQL and returns the abstract syntax tree (AST) structure with statement and table counts",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new
                                {
                                    type = "string",
                                    description = "The SQL query to parse"
                                },
                                sqlVersion = new
                                {
                                    type = "string",
                                    description = "SQL Server version (default: 160 for SQL Server 2022)",
                                    @enum = new[] { "90", "100", "110", "120", "130", "140", "150", "160" },
                                    @default = "160"
                                }
                            },
                            required = new[] { "query" }
                        }
                    }
                }
            }
        };
    }

    private object HandleToolCall(JsonNode request, string? id)
    {
        var toolName = request["params"]?["name"]?.GetValue<string>();
        var arguments = request["params"]?["arguments"];

        if (arguments == null)
        {
            throw new Exception("Missing arguments");
        }

        return toolName switch
        {
            "validate_sql" => HandleValidateSql(arguments, id),
            "parse_sql" => HandleParseSql(arguments, id),
            _ => new
            {
                jsonrpc = "2.0",
                id,
                error = new { code = -32602, message = $"Unknown tool: {toolName}" }
            }
        };
    }

    private TSqlParser GetParser(string? sqlVersion)
    {
        var version = sqlVersion ?? "160";
        
        return version switch
        {
            "90" => new TSql90Parser(true),
            "100" => new TSql100Parser(true),
            "110" => new TSql110Parser(true),
            "120" => new TSql120Parser(true),
            "130" => new TSql130Parser(true),
            "140" => new TSql140Parser(true),
            "150" => new TSql150Parser(true),
            "160" => new TSql160Parser(true),
            _ => new TSql160Parser(true) // Default to latest
        };
    }

    private object HandleValidateSql(JsonNode arguments, string? id)
    {
        var query = arguments["query"]?.GetValue<string>();
        if (string.IsNullOrEmpty(query))
        {
            throw new Exception("Query is required");
        }

        var sqlVersion = arguments["sqlVersion"]?.GetValue<string>();
        var parser = GetParser(sqlVersion);

        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var errors);

        var versionName = GetVersionName(sqlVersion ?? "160");

        var result = new
        {
            valid = errors.Count == 0,
            errorCount = errors.Count,
            sqlVersion = sqlVersion ?? "160",
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

        return new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(result, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        })
                    }
                }
            }
        };
    }

    private object HandleParseSql(JsonNode arguments, string? id)
    {
        var query = arguments["query"]?.GetValue<string>();
        if (string.IsNullOrEmpty(query))
        {
            throw new Exception("Query is required");
        }

        var sqlVersion = arguments["sqlVersion"]?.GetValue<string>();
        var parser = GetParser(sqlVersion);

        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var errors);

        var result = new
        {
            valid = errors.Count == 0,
            sqlVersion = sqlVersion ?? "160",
            sqlVersionName = GetVersionName(sqlVersion ?? "160"),
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

        return new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(result, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        })
                    }
                }
            }
        };
    }

    private string GetAstInfo(TSqlFragment fragment)
    {
        var visitor = new SqlFragmentVisitor();
        fragment.Accept(visitor);
        return $"Statements: {visitor.StatementCount}, Tables: {visitor.TableCount}";
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
