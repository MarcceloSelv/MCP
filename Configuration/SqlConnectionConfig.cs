using System.Text.Json;

public class SqlConnectionConfig
{
    public Dictionary<string, string> Databases { get; set; } = new();
    public string? DefaultDatabase { get; set; }

    public static SqlConnectionConfig LoadFromArgs(string[] args)
    {
        var config = new SqlConnectionConfig();

        // Prioridade 1: Variáveis de ambiente
        LoadFromEnvironment(config);

        // Prioridade 2: Argumentos de linha de comando (sobrescreve variáveis de ambiente se fornecido)
        LoadFromCommandLine(config, args);

        // Se não houver default, usa o primeiro
        if (config.DefaultDatabase == null && config.Databases.Count > 0)
        {
            config.DefaultDatabase = config.Databases.Keys.First();
        }

        // Log da configuração carregada (sem mostrar connection strings completas por segurança)
        Console.Error.WriteLine($"Loaded {config.Databases.Count} database(s): {string.Join(", ", config.Databases.Keys)}");
        Console.Error.WriteLine($"Default database: {config.DefaultDatabase ?? "not set"}");

        return config;
    }

    private static void LoadFromEnvironment(SqlConnectionConfig config)
    {
        // Opção 1: JSON completo em uma variável (SQL_MCP_DATABASES)
        var databasesJson = Environment.GetEnvironmentVariable("SQL_MCP_DATABASES");
        if (!string.IsNullOrEmpty(databasesJson))
        {
            try
            {
                var databases = JsonSerializer.Deserialize<Dictionary<string, string>>(databasesJson);
                if (databases != null && databases.Count > 0)
                {
                    config.Databases = databases;
                    Console.Error.WriteLine("Loaded databases from SQL_MCP_DATABASES environment variable");
                }
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error parsing SQL_MCP_DATABASES: {ex.Message}");
            }
        }

        // Opção 2: Variáveis individuais (SQL_MCP_DB_<NAME>=<connection_string>)
        var environmentVariables = Environment.GetEnvironmentVariables();
        foreach (var key in environmentVariables.Keys)
        {
            string keyStr = key.ToString() ?? "";
            if (keyStr.StartsWith("SQL_MCP_DB_", StringComparison.OrdinalIgnoreCase))
            {
                var dbName = keyStr.Substring("SQL_MCP_DB_".Length).ToLower();
                var connString = environmentVariables[key]?.ToString();

                if (!string.IsNullOrEmpty(dbName) && !string.IsNullOrEmpty(connString))
                {
                    config.Databases[dbName] = connString;
                    Console.Error.WriteLine($"Loaded database '{dbName}' from environment variable {keyStr}");
                }
            }
        }

        // Default database
        var defaultDb = Environment.GetEnvironmentVariable("SQL_MCP_DEFAULT_DATABASE");
        if (!string.IsNullOrEmpty(defaultDb))
        {
            config.DefaultDatabase = defaultDb;
        }
    }

    private static void LoadFromCommandLine(SqlConnectionConfig config, string[] args)
    {
        // Procura por argumentos no formato --databases={"db1":"conn1","db2":"conn2"} --default-database=db1
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--databases="))
            {
                var jsonDatabases = args[i].Substring("--databases=".Length);
                try
                {
                    var databases = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonDatabases);
                    if (databases != null)
                    {
                        config.Databases = databases;
                        Console.Error.WriteLine("Loaded databases from command-line arguments (overriding environment variables)");
                    }
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"Error parsing databases JSON from command-line: {ex.Message}");
                }
            }
            else if (args[i].StartsWith("--default-database="))
            {
                config.DefaultDatabase = args[i].Substring("--default-database=".Length);
            }
        }
    }

    public string? GetConnectionString(string? databaseName = null)
    {
        var dbName = databaseName ?? DefaultDatabase;

        if (string.IsNullOrEmpty(dbName))
        {
            return null;
        }

        return Databases.TryGetValue(dbName, out var connString) ? connString : null;
    }

    public List<string> GetAvailableDatabases()
    {
        return Databases.Keys.ToList();
    }
}
