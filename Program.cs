using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

class Program
{
    static async Task Main(string[] args)
    {
        var config = SqlConnectionConfig.LoadFromArgs(args);

        var builder = Host.CreateApplicationBuilder(args);

        // Register services
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton<SqlDocumentationService>();
        builder.Services.AddSingleton<SqlExecutionService>();
        builder.Services.AddSingleton<SqlMcpTools>();

        // Configure MCP Server
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "sql-mcp-server",
                    Version = "4.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(SqlMcpTools).Assembly);

        var app = builder.Build();

        await Console.Error.WriteLineAsync("SQL MCP Server v4.0 starting (using ModelContextProtocol library)...");
        await Console.Error.WriteLineAsync($"Default database: {config.DefaultDatabase ?? "not configured"}");
        await Console.Error.WriteLineAsync($"Available databases: {string.Join(", ", config.GetAvailableDatabases())}");

        await app.RunAsync();
    }
}
