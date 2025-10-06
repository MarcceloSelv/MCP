using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text.Json;
using System.Text.Json.Nodes;

class Program
{
    static async Task Main(string[] args)
    {
        var config = SqlConnectionConfig.LoadFromArgs(args);
        var server = new SqlMcpServer(config);
        await server.RunAsync();
    }
}

public class SqlMcpServer
{
    private readonly SqlDocumentationService _documentationService = new();
    private readonly SqlExecutionService _executionService;
    private readonly SqlConnectionConfig _config;

    public SqlMcpServer(SqlConnectionConfig config)
    {
        _config = config;
        _executionService = new SqlExecutionService(config);
    }

    public async Task RunAsync()
    {
        await Console.Error.WriteLineAsync("SQL MCP Server starting...");
        
        while (true)
        {
            var line = await Console.In.ReadLineAsync();
            if (line == null) break;
            
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            try
            {
                var request = JsonSerializer.Deserialize<JsonNode>(line);
                if (request == null)
                {
                    await WriteError(null, -32700, "Parse error");
                    continue;
                }
                
                var hasId = request["id"] != null;
                
                if (!hasId)
                {
                    await Console.Error.WriteLineAsync($"Received notification: {request["method"]?.GetValue<string>()}");
                    continue;
                }
                
                var requestId = ExtractId(request);
                var response = HandleRequest(request, requestId);
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response));
            }
            catch (JsonException)
            {
                await WriteError(null, -32700, "Parse error: Invalid JSON");
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            }
        }
    }

    private object? ExtractId(JsonNode request)
    {
        var idNode = request["id"];
        if (idNode == null) return null;
        
        try { return idNode.GetValue<int>(); }
        catch { try { return idNode.GetValue<string>(); } catch { return null; } }
    }

    private async Task WriteError(object? id, int code, string message)
    {
        var error = new { jsonrpc = "2.0", id, error = new { code, message } };
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(error));
    }

    private object HandleRequest(JsonNode request, object? id)
    {
        var method = request["method"]?.GetValue<string>();
        
        if (string.IsNullOrEmpty(method))
        {
            return new
            {
                jsonrpc = "2.0",
                id,
                error = new { code = -32600, message = "Invalid Request: method is required" }
            };
        }

        return method switch
        {
            "initialize" => HandleInitialize(id),
            "tools/list" => HandleToolsList(id),
            "tools/call" => HandleToolCall(request, id),
            _ => new { jsonrpc = "2.0", id, error = new { code = -32601, message = $"Method not found: {method}" } }
        };
    }

    private object HandleInitialize(object? id)
    {
        return new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new { name = "sql-mcp-server", version = "3.0.0" },
                capabilities = new { tools = new { } }
            }
        };
    }

    private object HandleToolsList(object? id)
    {
        var availableDatabases = string.Join(", ", _config.GetAvailableDatabases());
        var defaultDatabase = _config.DefaultDatabase ?? "not configured";

        return new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                tools = new object[]
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
                                query = new { type = "string", description = "The SQL query to validate" },
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
                                query = new { type = "string", description = "The SQL query to parse" },
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
                    },
                    new
                    {
                        name = "document_sql",
                        description = "Generates comprehensive Markdown documentation for SQL scripts including tables, functions, complexity analysis and recommendations",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new { type = "string", description = "The SQL script to document" },
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
                    },
                    new
                    {
                        name = "execute_sql",
                        description = "Executes SQL queries against configured databases. Only SELECT, INSERT, and other safe read/create commands are allowed. DROP, DELETE, UPDATE, TRUNCATE, and ALTER commands are blocked for safety.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new { type = "string", description = "The SQL query to execute" },
                                database = new
                                {
                                    type = "string",
                                    description = $"Database name to use (default: {defaultDatabase}). Available databases: {availableDatabases}"
                                }
                            },
                            required = new[] { "query" }
                        }
                    },
                    new
                    {
                        name = "list_databases",
                        description = "Lists all configured databases and shows which one is the default",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new { }
                        }
                    }
                }
            }
        };
    }

    private object HandleToolCall(JsonNode request, object? id)
    {
        try
        {
            var paramsNode = request["params"];
            if (paramsNode == null)
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Invalid params: params object is required" } };
            }

            var toolName = paramsNode["name"]?.GetValue<string>();
            if (string.IsNullOrEmpty(toolName))
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Invalid params: name is required" } };
            }

            var arguments = paramsNode["arguments"];
            if (arguments == null)
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Invalid params: arguments is required" } };
            }

            return toolName switch
            {
                "validate_sql" => HandleValidateSql(arguments, id),
                "parse_sql" => HandleParseSql(arguments, id),
                "document_sql" => HandleDocumentSql(arguments, id),
                "execute_sql" => HandleExecuteSql(arguments, id),
                "list_databases" => HandleListDatabases(id),
                _ => new { jsonrpc = "2.0", id, error = new { code = -32602, message = $"Unknown tool: {toolName}" } }
            };
        }
        catch (Exception ex)
        {
            return new { jsonrpc = "2.0", id, error = new { code = -32603, message = $"Internal error: {ex.Message}" } };
        }
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
            _ => new TSql160Parser(true)
        };
    }

    private object HandleValidateSql(JsonNode arguments, object? id)
    {
        try
        {
            var query = arguments["query"]?.GetValue<string>();
            if (string.IsNullOrEmpty(query))
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Invalid params: query is required" } };
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
                            text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new { jsonrpc = "2.0", id, error = new { code = -32603, message = $"Internal error: {ex.Message}" } };
        }
    }

    private object HandleParseSql(JsonNode arguments, object? id)
    {
        try
        {
            var query = arguments["query"]?.GetValue<string>();
            if (string.IsNullOrEmpty(query))
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Invalid params: query is required" } };
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
                            text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new { jsonrpc = "2.0", id, error = new { code = -32603, message = $"Internal error: {ex.Message}" } };
        }
    }

    private object HandleDocumentSql(JsonNode arguments, object? id)
    {
        try
        {
            var query = arguments["query"]?.GetValue<string>();
            if (string.IsNullOrEmpty(query))
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Invalid params: query is required" } };
            }

            var sqlVersion = arguments["sqlVersion"]?.GetValue<string>();
            var result = _documentationService.GenerateDocumentation(query, sqlVersion ?? "160");

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
                            text = result.Success ? result.MarkdownDocumentation : JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new { jsonrpc = "2.0", id, error = new { code = -32603, message = $"Internal error: {ex.Message}" } };
        }
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

    private object HandleExecuteSql(JsonNode arguments, object? id)
    {
        try
        {
            var query = arguments["query"]?.GetValue<string>();
            if (string.IsNullOrEmpty(query))
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Invalid params: query is required" } };
            }

            var database = arguments["database"]?.GetValue<string>();
            var result = _executionService.ExecuteQuery(query, database);

            // Formata o resultado
            var resultText = FormatExecutionResult(result);

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
                            text = resultText
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new { jsonrpc = "2.0", id, error = new { code = -32603, message = $"Internal error: {ex.Message}" } };
        }
    }

    private object HandleListDatabases(object? id)
    {
        try
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
                            text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new { jsonrpc = "2.0", id, error = new { code = -32603, message = $"Internal error: {ex.Message}" } };
        }
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

        // Sucesso - formata os resultados
        var output = new System.Text.StringBuilder();
        output.AppendLine($"✓ Query executed successfully on database: {result.DatabaseUsed}");
        output.AppendLine($"Rows returned: {result.RowsAffected}");
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

                // Formata como tabela Markdown
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
