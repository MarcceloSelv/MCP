using System.Text.Json;

public class SqlConnectionConfig
{
    public Dictionary<string, string> Databases { get; set; } = new();
    public string? DefaultDatabase { get; set; }

    public static SqlConnectionConfig LoadFromArgs(string[] args)
    {
        var config = new SqlConnectionConfig();

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
                    }
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"Error parsing databases JSON: {ex.Message}");
                }
            }
            else if (args[i].StartsWith("--default-database="))
            {
                config.DefaultDatabase = args[i].Substring("--default-database=".Length);
            }
        }

        // Se não houver default, usa o primeiro
        if (config.DefaultDatabase == null && config.Databases.Count > 0)
        {
            config.DefaultDatabase = config.Databases.Keys.First();
        }

        return config;
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
