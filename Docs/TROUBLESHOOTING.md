# Troubleshooting - SQL MCP Server

## ❌ Erro: "Error parsing databases JSON"

### Sintoma:
```
Error parsing databases JSON: 'Q' is an invalid start of a property name. Expected a '"'. Path: $ | LineNumber: 0 | BytePositionInLine: 1.
```

### Causa:
O Windows CMD/PowerShell não processa corretamente o escape de aspas duplas quando você passa JSON como argumento.

### ❌ Forma INCORRETA (gera erro):
```cmd
SqlMcpServer.exe --databases={"QA":"Server=..."}
```

O shell interpreta as aspas e remove os escapes, resultando em JSON inválido.

### ✅ Solução 1: Usar arquivo .bat

Crie um arquivo `start-mcp.bat`:

```batch
@echo off
set DATABASES={\"QA\":\"Server=10.0.0.181;Database=MRC_QA;User Id=sistema;Password=SYSUSER;TrustServerCertificate=True\",\"Norcoast\":\"Server=10.0.51.11;Database=ECARGO;User Id=SISTEMA;Password=SYSUSER;TrustServerCertificate=True\"}

SqlMcpServer.exe --databases=%DATABASES% --default-database=QA
```

### ✅ Solução 2: Usar no Claude Desktop (Recomendado)

O Claude Desktop faz o escape correto automaticamente:

```json
{
  "mcpServers": {
    "sql-mcp-server": {
      "command": "C:\\caminho\\para\\SqlMcpServer.exe",
      "args": [
        "--databases={\"QA\":\"Server=10.0.0.181;Database=MRC_QA;User Id=sistema;Password=SYSUSER;TrustServerCertificate=True\",\"Norcoast\":\"Server=10.0.51.11;Database=ECARGO;User Id=SISTEMA;Password=SYSUSER;TrustServerCertificate=True\"}",
        "--default-database=QA"
      ]
    }
  }
}
```

### ✅ Solução 3: PowerShell

No PowerShell, use aspas simples externas:

```powershell
.\SqlMcpServer.exe '--databases={\"QA\":\"Server=10.0.0.181;Database=MRC_QA;User Id=sistema;Password=SYSUSER;TrustServerCertificate=True\"}' --default-database=QA
```

---

## 🧪 Testando Localmente

Use o script fornecido:

```cmd
test-mcp.bat
```

Isso iniciará o servidor corretamente. Você pode então enviar comandos JSON-RPC via stdin:

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
```

---

## ✅ Verificação Rápida

Se o servidor iniciar e mostrar:
```
SQL MCP Server starting...
```

E aguardar entrada sem erros, está funcionando corretamente!

---

## 🔍 Debug

Para verificar se os argumentos foram parseados corretamente, adicione este código temporário ao `Program.cs`:

```csharp
Console.Error.WriteLine($"Databases loaded: {config.GetAvailableDatabases().Count}");
Console.Error.WriteLine($"Default: {config.DefaultDatabase}");
```

Isso mostrará no stderr se a configuração foi carregada.
