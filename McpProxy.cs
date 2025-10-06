using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

public class SqlValidatorMcpProxyServer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public SqlValidatorMcpProxyServer()
    {
        _httpClient = new HttpClient();
        // URL da API no Kubernetes
        _apiBaseUrl = Environment.GetEnvironmentVariable("SQL_VALIDATOR_API_URL") 
            ?? "http://sql-validator-api.default.svc.cluster.local:8080";
    }

    public async Task RunAsync()
    {
        await Console.Error.WriteLineAsync("SQL Validator MCP Proxy starting...");
        
        while (true)
        {
            var line = await Console.In.ReadLineAsync();
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            try
            {
                var request = JsonSerializer.Deserialize<JsonNode>(line);
                if (request == null) continue;
                
                var hasId = request["id"] != null;
                if (!hasId) continue; // Ignora notificações
                
                var requestId = ExtractId(request);
                var response = await HandleRequest(request, requestId);
                
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            }
        }
    }

    private async Task<object> HandleRequest(JsonNode request, object? id)
    {
        var method = request["method"]?.GetValue<string>();
        
        return method switch
        {
            "initialize" => HandleInitialize(id),
            "tools/list" => HandleToolsList(id),
            "tools/call" => await HandleToolCallViaApi(request, id),
            _ => new
            {
                jsonrpc = "2.0",
                id,
                error = new { code = -32601, message = $"Method not found: {method}" }
            }
        };
    }

    private async Task<object> HandleToolCallViaApi(JsonNode request, object? id)
    {
        try
        {
            var paramsNode = request["params"];
            var toolName = paramsNode?["name"]?.GetValue<string>();
            var arguments = paramsNode?["arguments"];

            // Faz requisição HTTP para API no Kubernetes
            var apiRequest = new
            {
                tool = toolName,
                arguments = JsonSerializer.Serialize(arguments)
            };

            var httpResponse = await _httpClient.PostAsync(
                $"{_apiBaseUrl}/api/validate",
                JsonContent.Create(apiRequest)
            );

            var result = await httpResponse.Content.ReadAsStringAsync();

            return new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    content = new[]
                    {
                        new { type = "text", text = result }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                jsonrpc = "2.0",
                id,
                error = new { code = -32603, message = $"API error: {ex.Message}" }
            };
        }
    }

    private object? ExtractId(JsonNode request)
    {
        var idNode = request["id"];
        if (idNode == null) return null;
        
        try { return idNode.GetValue<int>(); }
        catch { try { return idNode.GetValue<string>(); } catch { return null; } }
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
                serverInfo = new { name = "sql-validator-mcp-proxy", version = "1.0.0" },
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
                        description = "Validates SQL Server T-SQL syntax (via Kubernetes API)",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new { type = "string", description = "The SQL query to validate" }
                            },
                            required = new[] { "query" }
                        }
                    }
                }
            }
        };
    }
}
