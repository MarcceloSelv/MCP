using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text.Json;
using System.Text.Json.Nodes;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new SqlValidatorMcpServer();
        await server.RunAsync();

        //// MCP Server que faz proxy para API no Kubernetes
        //var server = new SqlValidatorMcpProxyServer();
        //await server.RunAsync();
    }
}

public class SqlValidatorMcpServer
{
    private readonly SqlFormatterService _formatterService = new();
    private readonly SqlDocumentationService _documentationService = new();

    public async Task RunAsync()
    {
        await Console.Error.WriteLineAsync("SQL Validator MCP Server starting...");
        
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
                serverInfo = new { name = "sql-validator-mcp", version = "2.0.0" },
                capabilities = new { tools = new { } }
            }
        };
    }

    private object HandleToolsList(object? id)
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
                        name = "format_sql",
                        description = "Formats and beautifies SQL code with proper indentation and structure",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new { type = "string", description = "The SQL query to format" },
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
                "format_sql" => HandleFormatSql(arguments, id),
                "document_sql" => HandleDocumentSql(arguments, id),
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

    private object HandleFormatSql(JsonNode arguments, object? id)
    {
        try
        {
            var query = arguments["query"]?.GetValue<string>();
            if (string.IsNullOrEmpty(query))
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32602, message = "Invalid params: query is required" } };
            }

            var sqlVersion = arguments["sqlVersion"]?.GetValue<string>();
            var result = _formatterService.FormatSql(query, sqlVersion ?? "160");

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
