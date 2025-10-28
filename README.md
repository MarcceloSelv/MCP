# SQL MCP Server v3.1

MCP Server completo para SQL Server: validação, execução segura e documentação de T-SQL usando o parser oficial da Microsoft.

## 🌟 Características

- ✅ **Validação de sintaxe** T-SQL (SQL Server 2005-2022)
- 🚀 **Execução segura de queries** em múltiplos bancos de dados
- 📚 **Geração automática de documentação** em Markdown
- 📊 Análise detalhada de erros (linha, coluna, mensagem)
- 🌳 Parsing de AST (Abstract Syntax Tree)
- 🔧 Compatível com múltiplas versões do SQL Server
- 🔒 **Segurança**: Bloqueia comandos destrutivos (DROP, DELETE, UPDATE, etc.)

## 📦 Instalação

```bash
cd C:\Users\Marccelo\source\repos\SQL-Server\Mcp
dotnet restore
dotnet build
dotnet publish -c Release -o ./publish
```

## ⚙️ Configuração no Claude Desktop

Existem **duas formas** de configurar os bancos de dados: via **variáveis de ambiente** (recomendado) ou via **argumentos de linha de comando**.

### Opção 1: Variáveis de Ambiente (Recomendado) ⭐

Configure as variáveis de ambiente do sistema e use uma configuração simples no Claude Desktop:

**Configuração das variáveis de ambiente (Windows):**

Método A - JSON completo:
```bash
# PowerShell (Usuário atual)
[Environment]::SetEnvironmentVariable("SQL_MCP_DATABASES", '{"qa":"Server=localhost;Database=QA;Integrated Security=true;","production":"Server=prod-server;Database=MyDB;User Id=user;Password=pass;"}', "User")
[Environment]::SetEnvironmentVariable("SQL_MCP_DEFAULT_DATABASE", "qa", "User")
```

Método B - Variáveis individuais:
```bash
# PowerShell (Usuário atual)
[Environment]::SetEnvironmentVariable("SQL_MCP_DB_QA", "Server=localhost;Database=QA;Integrated Security=true;", "User")
[Environment]::SetEnvironmentVariable("SQL_MCP_DB_PRODUCTION", "Server=prod-server;Database=MyDB;User Id=user;Password=pass;", "User")
[Environment]::SetEnvironmentVariable("SQL_MCP_DEFAULT_DATABASE", "qa", "User")
```

**Configuração no `claude_desktop_config.json`:**
```json
{
  "mcpServers": {
    "sql-mcp-server": {
      "command": "dotnet",
      "args": [
        "C:\\Users\\Marccelo\\source\\repos\\SQL-Server\\Mcp\\publish\\SqlMcpServer.dll"
      ]
    }
  }
}
```

### Opção 2: Argumentos de Linha de Comando

**Windows (Com múltiplos bancos de dados via args):**
```json
{
  "mcpServers": {
    "sql-mcp-server": {
      "command": "dotnet",
      "args": [
        "C:\\Users\\Marccelo\\source\\repos\\SQL-Server\\Mcp\\publish\\SqlMcpServer.dll",
        "--databases={\"production\":\"Server=prod-server;Database=MyDB;User Id=user;Password=pass;\",\"staging\":\"Server=staging-server;Database=MyDB;User Id=user;Password=pass;\",\"development\":\"Server=localhost;Database=MyDB;Integrated Security=true;\"}",
        "--default-database=development"
      ]
    }
  }
}
```

**Localização:** `%APPDATA%\Claude\claude_desktop_config.json`

### Variáveis de Ambiente Disponíveis

- **`SQL_MCP_DATABASES`**: JSON com mapeamento de nome → connection string
  - Exemplo: `{"dev":"Server=localhost;Database=DB;...","prod":"Server=..."}`
- **`SQL_MCP_DB_<NAME>`**: Connection string para um banco específico
  - Exemplo: `SQL_MCP_DB_QA`, `SQL_MCP_DB_PRODUCTION`
  - O nome do banco será convertido para lowercase
- **`SQL_MCP_DEFAULT_DATABASE`**: Nome do banco padrão (opcional)

### Parâmetros de Linha de Comando

- `--databases=<json>`: JSON com mapeamento de nome → connection string (sobrescreve variáveis de ambiente)
- `--default-database=<nome>`: Nome do banco padrão (opcional, usa o primeiro se não especificado)

**Nota:** Argumentos de linha de comando têm prioridade sobre variáveis de ambiente.

## 🔄 Retry Policy e Timeouts

O servidor implementa políticas robustas de retry e timeout:

### Retry Automático
- **Erros transientes** são automaticamente retentados com **exponential backoff**
- **Máximo de 3 tentativas** (total de 4 execuções)
- **Delays**: 1s, 2s, 4s entre tentativas
- **Erros que acionam retry**:
  - Transport-level errors (conexão fechada pelo host remoto)
  - Connection broken / timeout
  - Network errors
  - Azure SQL transient errors (40197, 40501, 40613)

### Timeouts Configurados
- **Connection Timeout**: 15 segundos (tempo para estabelecer conexão)
- **Command Timeout**: 300 segundos / 5 minutos (tempo de execução da query)
- **Validation Timeout**: 5 segundos (validação rápida com `SET PARSEONLY ON`)

Erros não-transientes (sintaxe, permissões, etc.) **não são retentados**.

## 🛠️ Ferramentas Disponíveis

### 1. validate_sql
Valida a sintaxe de uma query SQL.

**Parâmetros:**
- `query` (string, obrigatório): A query SQL para validar
- `sqlVersion` (string, opcional): Versão do SQL Server (padrão: 160)

**Exemplo:**
```sql
SELECT * FROM Users WHERE Id = 1
```

**Resposta:**
```json
{
  "valid": true,
  "errorCount": 0,
  "errors": [],
  "summary": "✓ SQL syntax is valid (validated against SQL Server 2022)"
}
```

---

### 2. parse_sql
Faz o parsing da query e retorna informações sobre a estrutura AST.

**Parâmetros:**
- `query` (string, obrigatório): A query SQL para fazer parse
- `sqlVersion` (string, opcional): Versão do SQL Server

**Resposta:**
```json
{
  "valid": true,
  "fragmentType": "TSqlScript",
  "scriptTokenStream": 45,
  "astInfo": "Statements: 3, Tables: 2"
}
```

---

### 3. document_sql
Gera documentação Markdown completa para scripts SQL.

**Parâmetros:**
- `query` (string, obrigatório): O script SQL para documentar
- `sqlVersion` (string, opcional): Versão do SQL Server

---

### 4. execute_sql ✨ NOVO
Executa queries SQL em bancos de dados configurados com **validação de segurança**.

**Parâmetros:**
- `query` (string, obrigatório): A query SQL para executar
- `database` (string, opcional): Nome do banco de dados (usa o padrão se não especificado)

**Comandos Permitidos:**
- ✅ SELECT
- ✅ INSERT
- ✅ CREATE (tabelas, índices, etc.)

**Comandos Bloqueados:**
- ❌ DROP
- ❌ DELETE
- ❌ UPDATE
- ❌ TRUNCATE
- ❌ ALTER TABLE
- ❌ ALTER DATABASE

**Exemplo:**
```sql
SELECT TOP 10 CustomerID, CompanyName, ContactName
FROM Customers
ORDER BY CustomerID
```

**Resposta:**
```
✓ Query executed successfully on database: development
Rows returned: 10

| CustomerID | CompanyName | ContactName |
| --- | --- | --- |
| 1 | Alfreds Futterkiste | Maria Anders |
| 2 | Ana Trujillo | Ana Trujillo |
...
```

---

### 5. list_databases ✨ NOVO
Lista todos os bancos de dados configurados.

**Resposta:**
```json
{
  "success": true,
  "defaultDatabase": "development",
  "availableDatabases": ["production", "staging", "development"]
}
```

## 🎯 Comandos SQL Suportados

O parser suporta **TODOS** os comandos T-SQL:

- ✅ **DML**: SELECT, INSERT, UPDATE, DELETE, MERGE, BULK INSERT
- ✅ **DDL**: CREATE/ALTER/DROP (TABLE, INDEX, VIEW, PROCEDURE, FUNCTION, TRIGGER)
- ✅ **DCL**: GRANT, REVOKE, DENY
- ✅ **TCL**: BEGIN TRANSACTION, COMMIT, ROLLBACK
- ✅ **Administrativos**: DBCC, BACKUP, RESTORE, ALTER DATABASE
- ✅ **Avançados**: Extended Events, XML, JSON, CTEs, Window Functions, Temporal Tables, Graph Tables

## 📚 Exemplos de Uso via Claude

### Validar SQL
```
Claude, valide esta query:
SELECT * FROM Users WHERE Name LIKE '%test%'
```

### Executar Query
```
Claude, execute esta query no banco de development:
SELECT TOP 10 * FROM Customers ORDER BY CustomerID
```

### Listar Bancos Disponíveis
```
Claude, liste os bancos de dados disponíveis
```

### Documentar Stored Procedure
```
Claude, documente este procedure:
CREATE PROCEDURE UpdateUserStatus @UserId INT, @NewStatus BIT AS
BEGIN UPDATE Users SET Active = @NewStatus WHERE Id = @UserId END
```

## 🔢 Versões SQL Server Suportadas

| Código | Versão |
|--------|--------|
| 90 | SQL Server 2005 |
| 100 | SQL Server 2008 |
| 110 | SQL Server 2012 |
| 120 | SQL Server 2014 |
| 130 | SQL Server 2016 |
| 140 | SQL Server 2017 |
| 150 | SQL Server 2019 |
| 160 | SQL Server 2022 (padrão) |

## 🧪 Testando Localmente

Execute o programa de teste:
```bash
cd C:\Users\Marccelo\source\repos\SQL-Server\Mcp
dotnet run
```

## ✅ Mudanças da Versão Anterior

### v3.0 → v3.1 (Atual)

**Adicionado:**
- ✅ **Retry Policy**: Retry automático com exponential backoff para erros transientes
- ✅ **Timeouts Configuráveis**: Connection timeout (15s) separado do command timeout (300s)
- ✅ **Variáveis de Ambiente**: Configuração via `SQL_MCP_DATABASES` e `SQL_MCP_DB_<NAME>`
- ✅ **Logging Aprimorado**: Logs de retry e configuração carregada

### v2.0 → v3.0

**Removido:**
- ❌ **format_sql**: Ferramenta de formatação SQL removida
- ❌ **SqlFormatterService**: Serviço de formatação removido

**Adicionado:**
- ✅ **execute_sql**: Execução segura de queries SQL
- ✅ **list_databases**: Listagem de bancos configurados
- ✅ **SqlConnectionConfig**: Gerenciamento de múltiplas conexões
- ✅ **SqlExecutionService**: Serviço de execução com validação de segurança
- ✅ **Validação AST**: Bloqueia comandos perigosos antes da execução

## 🚀 Próximas Features (Planejadas)

- [ ] Validação semântica com conexão ao banco (SMO)
- [ ] Extração de tabelas/funções da query
- [ ] Verificação de existência de objetos no banco
- [ ] SQL Security Scanner (detecção de SQL Injection)
- [ ] Query Optimizer (sugestões de performance)
- [ ] SQL to LINQ Converter
- [ ] Transaction support (BEGIN/COMMIT/ROLLBACK)
- [ ] Migration Script Generator

## 📖 Bibliotecas Utilizadas

- **Microsoft.SqlServer.TransactSql.ScriptDom** v170.128.0 - Parser oficial T-SQL da Microsoft
- **Microsoft.Data.SqlClient** v6.1.1 - Cliente SQL Server oficial
- **System.Text.Json** v9.0.9 - Serialização JSON

## 📦 Build e Deployment

### Tamanhos de Distribuição

| Configuração | Tamanho | Observações |
|-------------|---------|-------------|
| **Self-contained (Recomendado)** | ~80MB | Não requer .NET instalado |
| **Framework-dependent** | ~10MB | Requer .NET 8 Runtime instalado |

**Nota sobre Trimming**: Trimming foi desabilitado porque interfere com JSON serialization dinâmica usada pelo MCP protocol. O tamanho adicional (~40MB) é aceitável para garantir compatibilidade total.

## 🎓 O que o Parser Valida

### ✅ Valida (Sintaxe)
- Palavras-chave SQL corretas
- Estrutura de comandos
- Vírgulas, parênteses, aspas
- Sintaxe de funções

### ❌ NÃO Valida (Semântica)
- Se funções existem
- Se tabelas existem
- Se colunas existem
- Tipos de dados compatíveis
- Permissões de usuário

**Para validação semântica completa, é necessário conexão com o banco de dados!**

## 📝 Comunicação MCP

O MCP Server se comunica via **stdin/stdout** usando protocolo **JSON-RPC 2.0**:

```
Claude Desktop ─stdin─> MCP Server
                       (lê JSON line-by-line)

Claude Desktop <─stdout─ MCP Server
                       (escreve JSON line-by-line)
```

Cada linha é uma mensagem JSON-RPC completa.

## 🐳 Deploy com Docker

```bash
docker build -t sql-validator-mcp .
docker run -i sql-validator-mcp
```

## ☸️ Deploy no Kubernetes

```bash
kubectl apply -f kubernetes-deployment.yaml
```

**Nota:** MCP usa stdin/stdout, então não funciona diretamente no K8s. Para usar em K8s, crie uma API HTTP que envolve o MCP Server.

## 📄 Licença

MIT

## 👤 Autor

Desenvolvido para uso com Claude AI via Model Context Protocol (MCP)
