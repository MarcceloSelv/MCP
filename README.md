# SQL Validator MCP Server

MCP Server para validação de sintaxe T-SQL usando o parser oficial da Microsoft.

## Características

- ✅ Validação de sintaxe T-SQL (SQL Server)
- 📊 Análise detalhada de erros (linha, coluna, mensagem)
- 🌳 Parsing de AST (Abstract Syntax Tree)
- 🔧 Compatível com múltiplas versões do SQL Server (2008-2022)

## Comandos SQL Suportados

O parser suporta **TODOS** os comandos T-SQL, incluindo:

### DML (Data Manipulation Language)
- SELECT, INSERT, UPDATE, DELETE, MERGE
- BULK INSERT

### DDL (Data Definition Language)
- CREATE/ALTER/DROP: TABLE, INDEX, VIEW, PROCEDURE, FUNCTION, TRIGGER
- CREATE/ALTER DATABASE
- CREATE/ALTER SCHEMA
- CREATE EVENT SESSION

### DCL (Data Control Language)
- GRANT, REVOKE, DENY
- CREATE/ALTER LOGIN, USER, ROLE

### TCL (Transaction Control)
- BEGIN TRANSACTION, COMMIT, ROLLBACK, SAVE TRANSACTION

### Comandos Administrativos
- DBCC (todos os comandos: CHECKDB, USEROPTIONS, etc.)
- BACKUP, RESTORE
- ALTER DATABASE

### Recursos Avançados
- XML: .value(), .query(), .nodes(), FOR XML
- JSON: FOR JSON, OPENJSON
- Extended Events
- Service Broker
- Full-Text Search
- Partitioning
- Temporal Tables
- Graph Tables

## Instalação

```bash
cd C:\Users\Marccelo\source\repos\SqlValidatorMcp
dotnet restore
dotnet build
dotnet publish -c Release -o ./publish
```

## Configuração no Claude Desktop

Adicione ao arquivo de configuração do Claude (`claude_desktop_config.json`):

**Windows:**
```json
{
  "mcpServers": {
    "sql-validator": {
      "command": "dotnet",
      "args": ["C:\\Users\\Marccelo\\source\\repos\\SqlValidatorMcp\\publish\\SqlValidatorMcp.dll"]
    }
  }
}
```

Localização do arquivo de configuração:
- **Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

## Ferramentas Disponíveis

### 1. validate_sql
Valida a sintaxe de uma query SQL.

**Parâmetros:**
- `query` (string, obrigatório): A query SQL para validar
- `sqlVersion` (string, opcional): Versão do SQL Server (padrão: 160)

**Exemplos de validação:**

#### DML Básico
```sql
SELECT * FROM Users WHERE Id = 1
```

#### CREATE TABLE
```sql
CREATE TABLE Customers (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE()
)
```

#### ALTER TABLE
```sql
ALTER TABLE Users 
ADD Email NVARCHAR(255) NULL,
    PhoneNumber VARCHAR(20) NULL
```

#### DBCC Commands
```sql
DBCC USEROPTIONS
DBCC CHECKDB (MyDatabase)
```

#### Extended Events
```sql
CREATE EVENT SESSION [QueryPerformance] ON SERVER 
ADD EVENT sqlserver.sql_statement_completed
ADD TARGET package0.event_file(SET filename=N'QueryPerf.xel')
WITH (MAX_MEMORY=4096 KB)
```

#### XML Methods
```sql
SELECT @TestXML.value('(/event/data[@name="cpu_time"]/value)[1]', 'bigint')
```

**Resposta para query válida:**
```json
{
  "valid": true,
  "errorCount": 0,
  "errors": [],
  "summary": "✓ SQL syntax is valid"
}
```

**Resposta para query inválida:**
```json
{
  "valid": false,
  "errorCount": 1,
  "errors": [
    {
      "line": 1,
      "column": 10,
      "message": "Incorrect syntax near 'FORM'.",
      "number": 46010,
      "offset": 9
    }
  ],
  "summary": "✗ Found 1 syntax error(s)"
}
```

### 2. parse_sql
Faz o parsing da query e retorna informações sobre a estrutura AST.

**Parâmetros:**
- `query` (string, obrigatório): A query SQL para fazer parse

## Versões SQL Server Suportadas

- `90` - SQL Server 2005
- `100` - SQL Server 2008
- `110` - SQL Server 2012
- `120` - SQL Server 2014
- `130` - SQL Server 2016
- `140` - SQL Server 2017
- `150` - SQL Server 2019
- `160` - SQL Server 2022 (padrão)

## Testando Localmente

Você pode testar o servidor diretamente via stdin/stdout:

```bash
cd C:\Users\Marccelo\source\repos\SqlValidatorMcp
dotnet run
```

Então envie requisições JSON-RPC via stdin:

```json
{"jsonrpc":"2.0","id":"1","method":"initialize","params":{}}
{"jsonrpc":"2.0","id":"2","method":"tools/list","params":{}}
{"jsonrpc":"2.0","id":"3","method":"tools/call","params":{"name":"validate_sql","arguments":{"query":"SELECT * FROM Users"}}}
```

## Bibliotecas Utilizadas

- **Microsoft.SqlServer.TransactSql.ScriptDom**: Parser oficial T-SQL da Microsoft
- **System.Text.Json**: Serialização JSON

## Como Funciona

1. O MCP Server recebe comandos via stdio (stdin/stdout)
2. Usa o `TSql160Parser` da Microsoft para fazer parse do SQL
3. Retorna erros detalhados com linha, coluna e mensagem
4. Pode ser usado por LLMs (como Claude) via protocolo MCP

## Licença

MIT
