using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Data;
using System.Text;

public class SqlExecutionService
{
    private readonly SqlConnectionConfig _config;
    private const int MaxRetries = 3;
    private const int ConnectionTimeoutSeconds = 15;
    private const int CommandTimeoutSeconds = 300;

    public SqlExecutionService(SqlConnectionConfig config)
    {
        _config = config;
    }

    public SqlExecutionResult ExecuteQuery(string query, string? databaseName = null)
    {
        // Implementa retry com exponential backoff
        int attempt = 0;
        Exception? lastException = null;

        while (attempt <= MaxRetries)
        {
            try
            {
                return ExecuteQueryInternal(query, databaseName);
            }
            catch (SqlException ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                lastException = ex;
                attempt++;

                // Exponential backoff: 1s, 2s, 4s
                int delayMs = (int)Math.Pow(2, attempt - 1) * 1000;

                Console.Error.WriteLine($"Transport error on attempt {attempt}/{MaxRetries + 1}: {ex.Message}. Retrying in {delayMs}ms...");
                Thread.Sleep(delayMs);
            }
            catch (Exception ex)
            {
                // Erros não-transientes não fazem retry
                lastException = ex;
                break;
            }
        }

        // Se chegou aqui, todas as tentativas falharam
        if (lastException is SqlException sqlEx)
        {
            return new SqlExecutionResult
            {
                Success = false,
                ErrorMessage = $"SQL Error after {attempt} attempts: {sqlEx.Message}",
                SqlErrorNumber = sqlEx.Number,
                SqlErrorLineNumber = sqlEx.LineNumber
            };
        }

        return new SqlExecutionResult
        {
            Success = false,
            ErrorMessage = $"Error after {attempt} attempts: {lastException?.Message ?? "Unknown error"}"
        };
    }

    private bool IsTransientError(SqlException ex)
    {
        // Erros transientes que justificam retry
        int[] transientErrorNumbers = {
            -2,     // Timeout
            -1,     // Connection broken
            2,      // Network error
            53,     // Connection initialization error
            64,     // Error occurred during login
            233,    // Connection initialization error
            10053,  // Transport-level error (connection aborted)
            10054,  // Connection forcibly closed by remote host
            10060,  // Network or instance-specific error
            40197,  // Service error processing request
            40501,  // Service is busy
            40613   // Database unavailable
        };

        // Verifica se o número do erro está na lista de transientes
        if (transientErrorNumbers.Contains(ex.Number))
            return true;

        // Verifica também a mensagem de erro para casos específicos
        string message = ex.Message.ToLower();
        return message.Contains("transport-level error") ||
               message.Contains("connection was forcibly closed") ||
               message.Contains("foi forçado o cancelamento") ||
               message.Contains("connection broken");
    }

    private SqlExecutionResult ExecuteQueryInternal(string query, string? databaseName = null)
    {
        // Variáveis que precisam estar disponíveis no catch
        string? connectionString = null;
        string fixedQuery = query;

        try
        {
            // Obtém a connection string primeiro
            connectionString = _config.GetConnectionString(databaseName);
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

            // AUTO-FIX: Adiciona colchetes em palavras reservadas usadas como alias
            fixedQuery = AutoFixReservedKeywords(query);
            bool wasAutoFixed = fixedQuery != query;

            // Valida query com SQL Server real para mensagens de erro melhores
            var validation = ValidateQuery(fixedQuery, connectionString);
            if (!validation.IsValid)
            {
                return new SqlExecutionResult
                {
                    Success = false,
                    ErrorMessage = validation.ErrorMessage,
                    BlockedCommands = validation.BlockedCommands
                };
            }

            // Executa a query
            // Adiciona Connection Timeout à connection string se não estiver presente
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (builder.ConnectTimeout == 15) // Valor padrão do .NET
            {
                builder.ConnectTimeout = ConnectionTimeoutSeconds;
            }

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = new SqlCommand(fixedQuery, connection);
            command.CommandTimeout = CommandTimeoutSeconds; // Timeout de execução (5 minutos)

            var result = new SqlExecutionResult
            {
                Success = true,
                DatabaseUsed = databaseName ?? _config.DefaultDatabase ?? "unknown",
                WasAutoFixed = wasAutoFixed,
                OriginalQuery = wasAutoFixed ? query : null
            };

            // Lista para capturar mensagens informativas (PRINT, STATISTICS IO, etc)
            var infoMessages = new List<string>();

            // Handler para capturar mensagens do SQL Server
            connection.InfoMessage += (sender, e) =>
            {
                foreach (SqlError error in e.Errors)
                {
                    infoMessages.Add(error.Message);
                }
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
            result.InfoMessages = infoMessages;

            return result;
        }
        catch (SqlException ex)
        {
            // Tenta enriquecer o erro com informações adicionais
            string enrichedError = EnrichSqlError(ex, fixedQuery, connectionString ?? "");

            return new SqlExecutionResult
            {
                Success = false,
                ErrorMessage = enrichedError,
                SqlErrorNumber = ex.Number,
                SqlErrorLineNumber = ex.LineNumber
            };
        }
        catch (Exception ex)
        {
            return new SqlExecutionResult
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}\n\n💡 Action: Please review and correct the query, then try again."
            };
        }
    }

    private string AutoFixReservedKeywords(string query)
    {
        // Por enquanto, retorna a query original
        // O auto-fix de palavras reservadas é complexo devido à hierarquia de tipos do ScriptDom
        // A mensagem de erro já orienta o Claude a adicionar colchetes
        return query;
    }

    private string EnrichSqlError(SqlException ex, string query, string connectionString)
    {
        var baseError = FormatSqlError(ex);

        // Erro 207: Invalid column - tenta obter colunas disponíveis
        if (ex.Number == 207)
        {
            var systemObjectInfo = TryGetSystemObjectColumns(query, connectionString);
            if (!string.IsNullOrEmpty(systemObjectInfo))
            {
                return baseError + "\n" + systemObjectInfo;
            }
        }

        return baseError;
    }

    private string? TryGetSystemObjectColumns(string query, string connectionString)
    {
        try
        {
            // Detecta se a query usa funções/tabelas de sistema
            if (!query.Contains("sys.", StringComparison.OrdinalIgnoreCase) &&
                !query.Contains("dm_", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Tenta obter as colunas usando sys.dm_exec_describe_first_result_set
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            // Obtém a versão do SQL Server
            var versionInfo = GetSqlServerVersion(connection);

            var describeQuery = $@"
                SELECT name, system_type_name, max_length, precision, scale
                FROM sys.dm_exec_describe_first_result_set(N'{query.Replace("'", "''")}', NULL, 0)
                WHERE is_hidden = 0
                ORDER BY column_ordinal";

            using var command = new SqlCommand(describeQuery, connection);
            command.CommandTimeout = 5;

            var columns = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var colName = reader.GetString(0);
                var colType = reader.GetString(1);
                columns.Add($"{colName} ({colType})");
            }

            if (columns.Count > 0)
            {
                var result = new StringBuilder();
                result.AppendLine();
                result.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                result.AppendLine("📋 AVAILABLE COLUMNS (auto-discovered):");
                result.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                foreach (var col in columns)
                {
                    result.AppendLine($"   ✓ {col}");
                }
                result.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                result.AppendLine();
                result.AppendLine($"💡 SQL Server Version: {versionInfo}");
                result.AppendLine("   For more details, consult Microsoft documentation:");
                result.AppendLine($"   https://learn.microsoft.com/sql/relational-databases/");
                result.AppendLine();
                result.AppendLine("🔄 Use one of the available columns above and retry.");

                return result.ToString();
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to auto-discover columns: {ex.Message}");
            return null;
        }
    }

    private string GetSqlServerVersion(SqlConnection connection)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT @@VERSION", connection);
            var version = cmd.ExecuteScalar()?.ToString() ?? "Unknown";

            // Extrai a versão principal (ex: "Microsoft SQL Server 2022...")
            var match = System.Text.RegularExpressions.Regex.Match(version, @"SQL Server (\d{4})");
            return match.Success ? $"SQL Server {match.Groups[1].Value}" : version.Split('\n')[0];
        }
        catch
        {
            return "Unknown";
        }
    }

    private string FormatSqlError(SqlException ex)
    {
        var errorMsg = new StringBuilder();
        errorMsg.AppendLine($"❌ SQL Error (Code {ex.Number}):");
        errorMsg.AppendLine($"   {ex.Message}");

        if (ex.LineNumber > 0)
        {
            errorMsg.AppendLine($"   Line: {ex.LineNumber}");
        }

        errorMsg.AppendLine();
        errorMsg.AppendLine("💡 Action Required:");

        // Mensagens específicas baseadas no código de erro
        switch (ex.Number)
        {
            case 207: // Invalid column name
                errorMsg.AppendLine("   - The column name is invalid or doesn't exist in the table");
                errorMsg.AppendLine("   - Check the column name spelling and case sensitivity");
                errorMsg.AppendLine();
                errorMsg.AppendLine("   For user tables:");
                errorMsg.AppendLine("     SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'YourTable'");
                errorMsg.AppendLine();
                errorMsg.AppendLine("   For system tables/functions (sys.*, dm_*, fn_*):");
                errorMsg.AppendLine("     EXEC sp_help 'sys.dm_db_log_info'");
                errorMsg.AppendLine("     -- OR --");
                errorMsg.AppendLine("     SELECT name, system_type_name FROM sys.dm_exec_describe_first_result_set(N'SELECT * FROM sys.dm_db_log_info(DB_ID())', NULL, 0)");
                errorMsg.AppendLine();
                errorMsg.AppendLine("   - Then retry with the correct column name");
                break;

            case 208: // Invalid object name
                var invalidObject = ExtractInvalidObjectName(ex.Message);

                errorMsg.AppendLine("   - The table/view/function name is invalid or doesn't exist");
                errorMsg.AppendLine("   - Check the object name spelling and schema");
                errorMsg.AppendLine();

                if (invalidObject != null && invalidObject.StartsWith("sys.", StringComparison.OrdinalIgnoreCase))
                {
                    errorMsg.AppendLine("   For system objects (sys.*):");
                    errorMsg.AppendLine("     SELECT name, type_desc FROM sys.objects WHERE name LIKE '%dm_db%'");
                    errorMsg.AppendLine("     SELECT name FROM sys.system_objects WHERE name LIKE '%log%'");
                }
                else
                {
                    errorMsg.AppendLine("   For user tables:");
                    errorMsg.AppendLine("     SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES");
                    errorMsg.AppendLine("     SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS TableName FROM sys.tables");
                }

                errorMsg.AppendLine();
                errorMsg.AppendLine("   - Then retry with the correct object name");
                break;

            case 156: // Incorrect syntax near keyword
                var keyword = ExtractKeywordFromError(ex.Message);

                errorMsg.AppendLine("   - There's a syntax error near a SQL keyword");
                errorMsg.AppendLine();

                if (!string.IsNullOrEmpty(keyword) && IsReservedKeyword(keyword))
                {
                    errorMsg.AppendLine($"   ⚠️  LIKELY CAUSE: '{keyword}' is a SQL reserved keyword used as an alias");
                    errorMsg.AppendLine($"   ✅  SOLUTION: Enclose it in square brackets: [{keyword}]");
                    errorMsg.AppendLine();
                    errorMsg.AppendLine($"   Example:");
                    errorMsg.AppendLine($"     ❌ ... as {keyword}");
                    errorMsg.AppendLine($"     ✅ ... as [{keyword}]");
                    errorMsg.AppendLine();
                }

                errorMsg.AppendLine("   Other possible causes:");
                errorMsg.AppendLine("   - Missing commas, parentheses, or quotes");
                errorMsg.AppendLine("   - Incorrect SQL syntax for the SQL Server version");
                errorMsg.AppendLine();
                errorMsg.AppendLine("   - Correct the syntax and retry");
                break;

            case 102: // Incorrect syntax
                errorMsg.AppendLine("   - General syntax error detected");
                errorMsg.AppendLine("   - Review the query for typos or incorrect SQL syntax");
                errorMsg.AppendLine("   - Check for missing keywords or incorrect punctuation");
                errorMsg.AppendLine("   - Fix the syntax error and retry");
                break;

            case 4104: // Multi-part identifier could not be bound
                errorMsg.AppendLine("   - A column reference is ambiguous or doesn't exist in the specified table");
                errorMsg.AppendLine("   - Use table aliases to clarify column references (e.g., t1.ColumnName)");
                errorMsg.AppendLine("   - Verify the column exists in the table you're referencing");
                errorMsg.AppendLine("   - Correct the column reference and retry");
                break;

            case 1776: // Cannot use ORDER BY with TOP clause
            case 1778: // Column not allowed in ORDER BY
                errorMsg.AppendLine("   - There's an issue with the ORDER BY clause");
                errorMsg.AppendLine("   - Review the ORDER BY syntax and column names");
                errorMsg.AppendLine("   - Ensure columns in ORDER BY exist in the result set");
                errorMsg.AppendLine("   - Fix the ORDER BY clause and retry");
                break;

            default:
                errorMsg.AppendLine("   - Review the error message above carefully");
                errorMsg.AppendLine("   - Correct the identified issue in your query");
                errorMsg.AppendLine("   - Retry the corrected query");
                break;
        }

        errorMsg.AppendLine();
        errorMsg.AppendLine("🔄 Please fix the error and execute the corrected query.");

        return errorMsg.ToString();
    }

    private string? ExtractInvalidObjectName(string errorMessage)
    {
        // Extrai o nome do objeto de mensagens como: "Invalid object name 'sys.dm_db_log_info'."
        var match = System.Text.RegularExpressions.Regex.Match(errorMessage, @"Invalid object name '([^']+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? ExtractKeywordFromError(string errorMessage)
    {
        // Extrai a palavra-chave de mensagens como: "Incorrect syntax near the keyword 'rowcount'."
        var match = System.Text.RegularExpressions.Regex.Match(errorMessage, @"keyword '([^']+)'", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private bool IsReservedKeyword(string word)
    {
        // Lista de palavras reservadas comuns do SQL Server que são frequentemente usadas como alias
        var reservedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Palavras mais comuns como alias
            "COUNT", "ROWCOUNT", "ROW", "ROWS", "TOTAL", "SUM", "AVG", "MIN", "MAX",
            "INDEX", "KEY", "ORDER", "GROUP", "RANK", "PERCENT", "VALUE", "VALUES",
            "DATE", "TIME", "TIMESTAMP", "YEAR", "MONTH", "DAY", "LEVEL",
            "USER", "SCHEMA", "TABLE", "VIEW", "COLUMN", "DATABASE",
            "TYPE", "SIZE", "STATUS", "STATE", "MODE", "OPTION",

            // Outras palavras reservadas
            "SELECT", "FROM", "WHERE", "JOIN", "INNER", "OUTER", "LEFT", "RIGHT",
            "ON", "AND", "OR", "NOT", "IN", "EXISTS", "BETWEEN", "LIKE",
            "IS", "NULL", "AS", "BY", "HAVING", "DISTINCT", "UNION",
            "INSERT", "INTO", "CREATE", "ALTER", "DROP", "TRUNCATE",
            "BEGIN", "END", "IF", "ELSE", "WHILE", "CASE", "WHEN", "THEN",
            "DECLARE", "SET", "EXEC", "EXECUTE", "RETURN", "PRINT",
            "WITH", "OVER", "PARTITION", "OFFSET", "FETCH", "TOP"
        };

        return reservedKeywords.Contains(word);
    }

    private QueryValidation ValidateQuery(string query, string? connectionString = null)
    {
        var parser = new TSql160Parser(true);
        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var errors);

        // Primeiro verifica comandos bloqueados (mais rápido e não precisa de DB)
        var visitor = new DangerousCommandVisitor();
        fragment.Accept(visitor);

        if (visitor.BlockedCommands.Count > 0)
        {
            var errorMsg = new StringBuilder();
            errorMsg.AppendLine($"🚫 Security Error: Query contains blocked commands: {string.Join(", ", visitor.BlockedCommands)}");
            errorMsg.AppendLine();
            errorMsg.AppendLine("Only the following commands are allowed:");
            errorMsg.AppendLine("   ✅ SELECT - Read data");
            errorMsg.AppendLine("   ✅ INSERT - Create new records");
            errorMsg.AppendLine("   ✅ CREATE - Create tables, indexes, procedures, etc.");
            errorMsg.AppendLine();
            errorMsg.AppendLine("Blocked commands:");
            errorMsg.AppendLine("   ❌ DROP, DELETE, UPDATE, TRUNCATE, ALTER");
            errorMsg.AppendLine();
            errorMsg.AppendLine("💡 Action: Rewrite your query using only allowed commands and retry.");

            return new QueryValidation
            {
                IsValid = false,
                ErrorMessage = errorMsg.ToString(),
                BlockedCommands = visitor.BlockedCommands
            };
        }

        // Se o parser local encontrou erros, tenta validar no SQL Server para erro mais específico
        if (errors.Count > 0 && !string.IsNullOrEmpty(connectionString))
        {
            var sqlServerError = ValidateWithSqlServer(query, connectionString);
            if (!string.IsNullOrEmpty(sqlServerError))
            {
                var errorMsg = new StringBuilder();
                errorMsg.AppendLine("❌ SQL Syntax Error:");
                errorMsg.AppendLine($"   {sqlServerError}");
                errorMsg.AppendLine();
                errorMsg.AppendLine("💡 Action: Review the error above, fix the syntax issue, and retry the query.");

                return new QueryValidation
                {
                    IsValid = false,
                    ErrorMessage = errorMsg.ToString()
                };
            }
        }

        // Fallback para erro do parser local
        if (errors.Count > 0)
        {
            var errorMsg = new StringBuilder();
            errorMsg.AppendLine("❌ SQL Syntax Errors:");
            foreach (var error in errors)
            {
                errorMsg.AppendLine($"   - {error.Message} (Line {error.Line}, Column {error.Column})");
            }
            errorMsg.AppendLine();
            errorMsg.AppendLine("💡 Action: Correct the syntax errors listed above and retry the query.");

            return new QueryValidation
            {
                IsValid = false,
                ErrorMessage = errorMsg.ToString()
            };
        }

        return new QueryValidation { IsValid = true };
    }

    private string? ValidateWithSqlServer(string query, string connectionString)
    {
        try
        {
            // Usa timeout de conexão menor para validação rápida
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = 5 // Validação rápida: 5 segundos para conectar
            };

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            // SET PARSEONLY ON faz o SQL Server validar sem executar
            using var command = new SqlCommand($"SET PARSEONLY ON; {query}; SET PARSEONLY OFF;", connection);
            command.CommandTimeout = 5; // Validação rápida: 5 segundos para executar
            command.ExecuteNonQuery();

            return null; // Sem erros
        }
        catch (SqlException ex)
        {
            // Retorna a mensagem de erro real do SQL Server
            return ex.Message;
        }
        catch
        {
            // Se falhar por qualquer outro motivo, retorna null para usar erro do parser
            return null;
        }
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
    public List<string> InfoMessages { get; set; } = new();
    public bool WasAutoFixed { get; set; }
    public string? OriginalQuery { get; set; }
}

public class ResultTable
{
    public List<string> Columns { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
}
