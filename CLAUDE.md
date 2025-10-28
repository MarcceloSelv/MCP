# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SQL MCP Server v3.1 is a Model Context Protocol (MCP) server that provides SQL Server T-SQL validation, safe execution, and documentation generation capabilities. It uses Microsoft's official T-SQL parser (ScriptDom) and communicates via JSON-RPC 2.0 over stdin/stdout.

**Key features in v3.1:**
- Automatic retry with exponential backoff for transient errors
- Separate connection and command timeouts
- Environment variable-based configuration

## Build Commands

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Build release version
dotnet build -c Release

# Publish self-contained executable (recommended)
dotnet publish -c Release -o ./publish

# Run the server (for testing via stdin/stdout)
dotnet run

# Run published executable with help
./SqlMcpServer.exe --help
```

## Testing

- **TestConsole project**: Located in `TestConsole/` directory for manual testing
- **Test scripts**: `Scripts/test-queries.sql` contains sample queries
- The server expects JSON-RPC 2.0 messages via stdin and responds via stdout

## Architecture

### MCP Communication Layer (Program.cs)

The main entry point implements the JSON-RPC 2.0 protocol:
- **HandleRequest**: Routes incoming JSON-RPC methods (initialize, tools/list, tools/call)
- **HandleToolCall**: Dispatches to specific tool handlers (validate_sql, parse_sql, document_sql, execute_sql, list_databases)
- **Communication**: Line-by-line JSON reading from stdin, writing to stdout
- Error logging goes to stderr (doesn't interfere with JSON-RPC communication)

### Core Services

**SqlConnectionConfig (Configuration/SqlConnectionConfig.cs)**
- Manages multiple database connections from **environment variables** or command-line arguments
- **Priority**: Environment variables first, then command-line args override if provided
- **Environment variables**:
  - `SQL_MCP_DATABASES`: JSON string with all databases
  - `SQL_MCP_DB_<NAME>`: Individual database connection strings (e.g., `SQL_MCP_DB_QA`)
  - `SQL_MCP_DEFAULT_DATABASE`: Default database name
- **Command-line args** (override env vars):
  - `--databases={"db1":"conn1","db2":"conn2"}`
  - `--default-database=dbname`
- Logs loaded configuration to stderr (without exposing full connection strings)

**SqlExecutionService (Services/SqlExecutionService.cs)**
- Executes SQL queries with security validation and automatic retry
- **Retry Policy**:
  - Up to 3 retries (4 total attempts) for transient errors
  - Exponential backoff: 1s, 2s, 4s delays
  - Detects transient errors: transport-level, connection broken, timeouts, Azure SQL errors
  - Logs retry attempts to stderr
  - Non-transient errors (syntax, permissions) are NOT retried
- **Timeouts**:
  - Connection timeout: 15 seconds (time to establish connection)
  - Command timeout: 300 seconds (time to execute query)
  - Validation timeout: 5 seconds (for `SET PARSEONLY ON` checks)
- **Security model**: Uses DangerousCommandVisitor to block DROP, DELETE, UPDATE, TRUNCATE, ALTER via AST inspection
- **Validation flow**:
  1. AST-based security check (fast, blocks dangerous commands)
  2. SQL Server `SET PARSEONLY ON` validation for better error messages
  3. Parser fallback if database unavailable
- Captures informational messages (PRINT, STATISTICS IO) via SqlConnection.InfoMessage event
- Limits result sets to 1000 rows for safety

**SqlDocumentationService (Services/SqlDocumentationService.cs)**
- Generates comprehensive Markdown documentation from SQL scripts
- Uses SqlDocumentationAnalyzer visitor pattern to traverse AST
- Tracks: statements, tables, joins, subqueries, CTEs, functions, procedures
- Calculates complexity scores and generates recommendations
- Extracts stored procedure parameters and table dependencies

### Parser Version Mapping

The `GetParser()` method maps SQL Server version codes to parser classes:
- "90" → TSql90Parser (SQL Server 2005)
- "100" → TSql100Parser (SQL Server 2008)
- "110" → TSql110Parser (SQL Server 2012)
- "120" → TSql120Parser (SQL Server 2014)
- "130" → TSql130Parser (SQL Server 2016)
- "140" → TSql140Parser (SQL Server 2017)
- "150" → TSql150Parser (SQL Server 2019)
- "160" → TSql160Parser (SQL Server 2022, default)

### Visitor Pattern Usage

The codebase extensively uses the Visitor pattern from ScriptDom:
- **SqlFragmentVisitor**: Base class for traversing the SQL AST
- **DangerousCommandVisitor**: Detects blocked SQL commands for security
- **SqlDocumentationAnalyzer**: Collects statistics for documentation generation
- Override `Visit()` methods for specific AST node types (e.g., `Visit(SelectStatement node)`)

## Available MCP Tools

1. **validate_sql**: Validates T-SQL syntax, returns detailed error information (line, column, message)
2. **parse_sql**: Returns AST structure with statement/table counts
3. **document_sql**: Generates comprehensive Markdown documentation
4. **execute_sql**: Executes queries with automatic security validation (SELECT, INSERT, CREATE allowed)
5. **list_databases**: Lists configured database connections

## Configuration Notes

- **Primary configuration method**: Environment variables (recommended)
  - Set `SQL_MCP_DATABASES` or individual `SQL_MCP_DB_<NAME>` variables
  - Set `SQL_MCP_DEFAULT_DATABASE` for default database
- **Alternative method**: Command-line arguments (overrides environment variables)
  - Pass `--databases` and `--default-database` in Claude Desktop config
- The server is configured via Claude Desktop's `claude_desktop_config.json`
- Database validation uses `SET PARSEONLY ON` to get SQL Server's actual error messages without executing
- Trimming is disabled (`<PublishTrimmed>false</PublishTrimmed>`) to preserve JSON serialization compatibility
- Configuration loading logs to stderr for debugging

## Key Dependencies

- **Microsoft.SqlServer.TransactSql.ScriptDom v170.128.0**: Official T-SQL parser
- **Microsoft.Data.SqlClient v6.1.1**: SQL Server client
- **System.Text.Json v9.0.9**: JSON serialization
- **.NET 8.0** target framework

## Security Considerations

- Blocked commands: DROP, DELETE, UPDATE, TRUNCATE, ALTER TABLE, ALTER DATABASE
- Allowed commands: SELECT, INSERT, CREATE (tables, indexes, procedures, etc.)
- Security validation happens via AST inspection before SQL execution
- Query results limited to 1000 rows maximum
- 5-minute execution timeout prevents runaway queries

## Important Implementation Details

- **Error handling**: Syntax errors from the parser include line/column numbers; SQL errors include SQL error number and line number
- **Retry mechanism**:
  - `ExecuteQuery()` wraps `ExecuteQueryInternal()` with retry logic
  - `IsTransientError()` checks both SQL error numbers and message text
  - Only transient errors trigger retry; syntax/permission errors fail immediately
  - Retry attempts and delays are logged to stderr
- **Multi-result sets**: The execution service handles multiple result sets from a single query
- **InfoMessage capture**: PRINT statements and server messages are captured and returned separately
- **Result formatting**: Results are formatted as Markdown tables for Claude's consumption
- **Parser initialization**: Each validation/parse operation creates a fresh parser instance with `initialQuotedIdentifiers: true`
- **Connection string manipulation**: SqlConnectionStringBuilder is used to inject timeouts without breaking existing connection strings
