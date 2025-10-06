# SQL Validator MCP Server v2.0

MCP Server para validação, formatação e documentação de T-SQL usando o parser oficial da Microsoft.

## 🌟 Características

- ✅ **Validação de sintaxe** T-SQL (SQL Server 2005-2022)
- 🎨 **Formatação/Beautifier** de código SQL
- 📚 **Geração automática de documentação** em Markdown
- 📊 Análise detalhada de erros (linha, coluna, mensagem)
- 🌳 Parsing de AST (Abstract Syntax Tree)
- 🔧 Compatível com múltiplas versões do SQL Server

## 📦 Instalação

```bash
cd C:\Users\Marccelo\source\repos\SqlValidatorMcp
dotnet restore
dotnet build
dotnet publish -c Release -o ./publish
```

## ⚙️ Configuração no Claude Desktop

Adicione ao arquivo `claude_desktop_config.json`:

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

**Localização:** `%APPDATA%\Claude\claude_desktop_config.json`

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

### 3. format_sql ✨ NOVO
Formata e embeleza código SQL com indentação e estrutura adequadas.

**Parâmetros:**
- `query` (string, obrigatório): A query SQL para formatar
- `sqlVersion` (string, opcional): Versão do SQL Server

**Exemplo de entrada:**
```sql
SELECT*FROM Users WHERE id=1AND active=1
```

**Resposta:**
```json
{
  "success": true,
  "formattedSql": "SELECT *\nFROM Users\nWHERE id = 1\n    AND active = 1",
  "stats": {
    "originalLines": 1,
    "formattedLines": 4,
    "originalLength": 44,
    "formattedLength": 52
  }
}
```

---

### 4. document_sql ✨ NOVO
Gera documentação Markdown completa para scripts SQL.

**Parâmetros:**
- `query` (string, obrigatório): O script SQL para documentar
- `sqlVersion` (string, opcional): Versão do SQL Server

**Exemplo de entrada:**
```sql
CREATE PROCEDURE GetActiveUsers
    @MinAge INT = 18
AS
BEGIN
    SELECT u.Name, u.Email, o.OrderCount
    FROM Users u
    LEFT JOIN (
        SELECT UserId, COUNT(*) as OrderCount
        FROM Orders
        GROUP BY UserId
    ) o ON u.Id = o.UserId
    WHERE u.Active = 1 AND u.Age >= @MinAge
END
```

**Resposta Markdown gerada:**
```markdown
# SQL Script Documentation

## 📊 Summary
- **Total Statements:** 1
- **Tables Referenced:** 2
- **Functions Used:** 1
- **Stored Procedures:** 1
- **Complexity Score:** 4/10

## 📦 Stored Procedures

### `GetActiveUsers`

**Parameters:**
- `@MinAge` (INT)

**Tables Used:**
- `Users`
- `Orders`

## 🔗 Join Analysis
- **Total Joins:** 1
- **LEFT JOINs:** 1

## 💡 Recommendations
- Consider adding indexes on join columns
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

### Formatar SQL
```
Claude, formate este SQL:
SELECT id,name,email FROM users WHERE active=1 AND age>18
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
cd C:\Users\Marccelo\source\repos\SqlValidatorMcp
run-tests.bat
```

## 🚀 Próximas Features (Planejadas)

- [ ] Validação semântica com conexão ao banco (SMO)
- [ ] Extração de tabelas/funções da query
- [ ] Verificação de existência de objetos no banco
- [ ] SQL Security Scanner (detecção de SQL Injection)
- [ ] Query Optimizer (sugestões de performance)
- [ ] SQL to LINQ Converter
- [ ] Migration Script Generator

## 📖 Bibliotecas Utilizadas

- **Microsoft.SqlServer.TransactSql.ScriptDom** v161.9.119 - Parser oficial T-SQL da Microsoft
- **System.Text.Json** v8.0.5 - Serialização JSON

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
