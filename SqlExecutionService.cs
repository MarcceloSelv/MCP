using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Data;
using System.Text;

public class SqlExecutionService
{
    private readonly SqlConnectionConfig _config;

    public SqlExecutionService(SqlConnectionConfig config)
    {
        _config = config;
    }

    public SqlExecutionResult ExecuteQuery(string query, string? databaseName = null)
    {
        try
        {
            // Valida se há comandos bloqueados
            var validation = ValidateQuery(query);
            if (!validation.IsValid)
            {
                return new SqlExecutionResult
                {
                    Success = false,
                    ErrorMessage = validation.ErrorMessage,
                    BlockedCommands = validation.BlockedCommands
                };
            }

            // Obtém a connection string
            var connectionString = _config.GetConnectionString(databaseName);
            if (string.IsNullOrEmpty(connectionString))
            {
                return new SqlExecutionResult
                {
                    Success = false,
                    ErrorMessage = databaseName == null
                        ? "No default database configured"
                        : $"Database '{databaseName}' not found. Available databases: {string.Join(", ", _config.GetAvailableDatabases())}"
                };
            }

            // Executa a query
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30; // 30 segundos de timeout

            var result = new SqlExecutionResult
            {
                Success = true,
                DatabaseUsed = databaseName ?? _config.DefaultDatabase ?? "unknown"
            };

            using var reader = command.ExecuteReader();

            // Lê os resultados
            var tables = new List<ResultTable>();
            do
            {
                var table = new ResultTable();

                // Obtém os nomes das colunas
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    table.Columns.Add(reader.GetName(i));
                }

                // Lê as linhas (limitado a 1000 linhas por segurança)
                int rowCount = 0;
                while (reader.Read() && rowCount < 1000)
                {
                    var row = new List<object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                    }
                    table.Rows.Add(row);
                    rowCount++;
                }

                if (table.Columns.Count > 0)
                {
                    tables.Add(table);
                }

            } while (reader.NextResult());

            result.ResultTables = tables;
            result.RowsAffected = tables.Sum(t => t.Rows.Count);

            return result;
        }
        catch (SqlException ex)
        {
            return new SqlExecutionResult
            {
                Success = false,
                ErrorMessage = $"SQL Error: {ex.Message}",
                SqlErrorNumber = ex.Number,
                SqlErrorLineNumber = ex.LineNumber
            };
        }
        catch (Exception ex)
        {
            return new SqlExecutionResult
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    private QueryValidation ValidateQuery(string query)
    {
        var parser = new TSql160Parser(true);
        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var errors);

        if (errors.Count > 0)
        {
            return new QueryValidation
            {
                IsValid = false,
                ErrorMessage = $"SQL syntax errors: {string.Join("; ", errors.Select(e => e.Message))}"
            };
        }

        // Verifica comandos bloqueados
        var visitor = new DangerousCommandVisitor();
        fragment.Accept(visitor);

        if (visitor.BlockedCommands.Count > 0)
        {
            return new QueryValidation
            {
                IsValid = false,
                ErrorMessage = $"Query contains blocked commands: {string.Join(", ", visitor.BlockedCommands)}. Only SELECT, INSERT, and other read/create commands are allowed.",
                BlockedCommands = visitor.BlockedCommands
            };
        }

        return new QueryValidation { IsValid = true };
    }
}

// Visitor para detectar comandos perigosos
public class DangerousCommandVisitor : TSqlFragmentVisitor
{
    public List<string> BlockedCommands { get; } = new();

    public override void Visit(DropObjectsStatement node)
    {
        BlockedCommands.Add("DROP");
        base.Visit(node);
    }

    public override void Visit(DeleteStatement node)
    {
        BlockedCommands.Add("DELETE");
        base.Visit(node);
    }

    public override void Visit(UpdateStatement node)
    {
        BlockedCommands.Add("UPDATE");
        base.Visit(node);
    }

    public override void Visit(TruncateTableStatement node)
    {
        BlockedCommands.Add("TRUNCATE");
        base.Visit(node);
    }

    public override void Visit(AlterTableStatement node)
    {
        BlockedCommands.Add("ALTER TABLE");
        base.Visit(node);
    }

    public override void Visit(AlterDatabaseStatement node)
    {
        BlockedCommands.Add("ALTER DATABASE");
        base.Visit(node);
    }
}

public class QueryValidation
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> BlockedCommands { get; set; } = new();
}

public class SqlExecutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? SqlErrorNumber { get; set; }
    public int? SqlErrorLineNumber { get; set; }
    public List<string> BlockedCommands { get; set; } = new();
    public string? DatabaseUsed { get; set; }
    public int RowsAffected { get; set; }
    public List<ResultTable> ResultTables { get; set; } = new();
}

public class ResultTable
{
    public List<string> Columns { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
}
