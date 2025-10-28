# 🚀 Guia de Setup - SQL MCP Server v3.1

Este guia mostra o processo completo de build, configuração e instalação do SQL MCP Server no Claude Desktop.

## 📋 Pré-requisitos

- .NET 8.0 SDK instalado
- Claude Desktop instalado
- SQL Server (local ou remoto) acessível

## 🔧 Passo 1: Build e Publish

### Opção A: Script Automático (Recomendado)

```bash
# Execute o script de build
cd C:\Users\Marccelo\source\repos\SQL-Server\Mcp
.\Scripts\build.bat
```

O script irá:
1. Restaurar dependências
2. Compilar o projeto
3. Publicar para `.\publish` (executável standalone)
4. Remover arquivos de localização (economia de ~3MB)

### Opção B: Manual

```bash
# Navegue até o diretório do projeto
cd C:\Users\Marccelo\source\repos\SQL-Server\Mcp

# Publish standalone (não requer .NET instalado)
dotnet publish SqlMcpServer.csproj -c Release -r win-x64 --self-contained true -o .\publish

# OU: Publish framework-dependent (requer .NET 8 instalado)
dotnet publish SqlMcpServer.csproj -c Release -o .\publish
```

**Resultado:** Executável em `.\publish\SqlMcpServer.exe`

## 🌍 Passo 2: Configurar Variáveis de Ambiente

### Opção A: Script Automático (Recomendado)

```bash
.\Scripts\setup-env.bat
```

O script oferece 4 opções:
1. **JSON completo** - Para múltiplos bancos de uma vez
2. **Variáveis individuais** - Configurar banco por banco
3. **Mostrar configuração** - Ver configuração atual
4. **Limpar configuração** - Remover todas as variáveis

### Opção B: Manual via PowerShell

**Método 1: JSON Completo**

```powershell
# Configurar múltiplos bancos de uma vez
[Environment]::SetEnvironmentVariable(
    "SQL_MCP_DATABASES",
    '{"qa":"Server=localhost;Database=QA;Integrated Security=true;TrustServerCertificate=True","prod":"Server=prod-server;Database=MyDB;User Id=user;Password=pass;TrustServerCertificate=True"}',
    "User"
)

# Definir banco padrão
[Environment]::SetEnvironmentVariable("SQL_MCP_DEFAULT_DATABASE", "qa", "User")
```

**Método 2: Variáveis Individuais**

```powershell
# Configurar cada banco separadamente
[Environment]::SetEnvironmentVariable(
    "SQL_MCP_DB_QA",
    "Server=localhost;Database=QA;Integrated Security=true;TrustServerCertificate=True",
    "User"
)

[Environment]::SetEnvironmentVariable(
    "SQL_MCP_DB_PRODUCTION",
    "Server=prod-server;Database=MyDB;User Id=user;Password=pass;TrustServerCertificate=True",
    "User"
)

[Environment]::SetEnvironmentVariable("SQL_MCP_DEFAULT_DATABASE", "qa", "User")
```

### Verificar Configuração

```powershell
# Ver todas as variáveis SQL_MCP
Get-ChildItem Env: | Where-Object Name -like "SQL_MCP*"
```

## ⚙️ Passo 3: Configurar Claude Desktop

### 3.1 Localizar o Arquivo de Configuração

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

Caminho completo: `C:\Users\SEU_USUARIO\AppData\Roaming\Claude\claude_desktop_config.json`

### 3.2 Escolher Método de Configuração

Existem **3 métodos** para configurar os bancos de dados:

---

#### **Método 1: Variáveis de Ambiente no arquivo JSON (Recomendado)** ⭐

Defina as variáveis de ambiente diretamente no `claude_desktop_config.json` usando a propriedade `env`:

**Opção A - JSON completo:**

```json
{
  "mcpServers": {
    "sql-mcp-server": {
      "command": "C:\\Users\\Marccelo\\source\\repos\\SQL-Server\\Mcp\\publish\\SqlMcpServer.exe",
      "args": [],
      "env": {
        "SQL_MCP_DATABASES": "{\"qa\":\"Server=localhost;Database=QA;Integrated Security=true;TrustServerCertificate=True\",\"prod\":\"Server=prod-server;Database=MyDB;User Id=user;Password=pass;TrustServerCertificate=True\"}",
        "SQL_MCP_DEFAULT_DATABASE": "qa"
      }
    }
  }
}
```

**Opção B - Variáveis individuais:**

```json
{
  "mcpServers": {
    "sql-mcp-server": {
      "command": "C:\\Users\\Marccelo\\source\\repos\\SQL-Server\\Mcp\\publish\\SqlMcpServer.exe",
      "args": [],
      "env": {
        "SQL_MCP_DB_QA": "Server=localhost;Database=QA;Integrated Security=true;TrustServerCertificate=True",
        "SQL_MCP_DB_PRODUCTION": "Server=prod-server;Database=MyDB;User Id=user;Password=pass;TrustServerCertificate=True",
        "SQL_MCP_DB_STAGING": "Server=staging-server;Database=MyDB;User Id=user;Password=pass;TrustServerCertificate=True",
        "SQL_MCP_DEFAULT_DATABASE": "qa"
      }
    }
  }
}
```

**Vantagens:**
- ✅ Não precisa configurar variáveis de ambiente do sistema Windows
- ✅ Configuração fica centralizada em um único arquivo
- ✅ Fácil de compartilhar e versionar (remova senhas antes!)
- ✅ Não precisa reiniciar o Windows ou executar scripts PowerShell

**Arquivo de exemplo:** `Scripts\claude_desktop_config_with_env.example.json`

---

#### **Método 2: Variáveis de Ambiente do Sistema**

Configure usando PowerShell e deixe o arquivo JSON simples:

```powershell
# Configure as variáveis de ambiente (executar uma vez)
.\Scripts\setup-env.bat
```

```json
{
  "mcpServers": {
    "sql-mcp-server": {
      "command": "C:\\Users\\Marccelo\\source\\repos\\SQL-Server\\Mcp\\publish\\SqlMcpServer.exe",
      "args": []
    }
  }
}
```

**Vantagens:**
- ✅ Mais seguro (variáveis não ficam no arquivo JSON)
- ✅ Pode ser compartilhado entre múltiplas aplicações

---

#### **Método 3: Argumentos de Linha de Comando**

Passe os bancos diretamente como argumentos:

```json
{
  "mcpServers": {
    "sql-mcp-server": {
      "command": "C:\\Users\\Marccelo\\source\\repos\\SQL-Server\\Mcp\\publish\\SqlMcpServer.exe",
      "args": [
        "--databases={\"qa\":\"Server=localhost;Database=QA;Integrated Security=true;TrustServerCertificate=True\",\"prod\":\"Server=prod-server;Database=MyDB;User Id=user;Password=pass;TrustServerCertificate=True\"}",
        "--default-database=qa"
      ]
    }
  }
}
```

**Vantagens:**
- ✅ Simples e direto
- ⚠️ Menos seguro (senhas visíveis no arquivo JSON)

---

**IMPORTANTE:**
- Atualize o caminho do `command` com o caminho completo do seu sistema
- Use barras invertidas duplas (`\\`) no caminho Windows
- **Método 1 (env)** é o mais prático e não requer configuração externa

## 🔄 Passo 4: Reiniciar Claude Desktop

1. Feche o Claude Desktop completamente
2. Abra novamente
3. O SQL MCP Server será carregado automaticamente

## ✅ Passo 5: Testar a Configuração

No Claude Desktop, teste com os seguintes comandos:

```
Claude, liste os bancos de dados disponíveis
```

Resposta esperada:
```json
{
  "success": true,
  "defaultDatabase": "qa",
  "availableDatabases": ["qa", "prod"]
}
```

```
Claude, valide esta query SQL:
SELECT * FROM Users WHERE Id = 1
```

```
Claude, execute esta query no banco qa:
SELECT TOP 5 * FROM INFORMATION_SCHEMA.TABLES
```

## 📊 Exemplos de Connection Strings

### Windows Authentication (Integrated Security)
```
Server=localhost;Database=QA;Integrated Security=true;TrustServerCertificate=True
```

### SQL Server Authentication
```
Server=localhost;Database=QA;User Id=sa;Password=MyPassword123;TrustServerCertificate=True
```

### Azure SQL Database
```
Server=myserver.database.windows.net;Database=MyDB;User Id=admin@myserver;Password=MyPassword123;Encrypt=True
```

### Named Instance (SQL Express)
```
Server=localhost\\SQLEXPRESS;Database=QA;Integrated Security=true;TrustServerCertificate=True
```

### Remote Server with Port
```
Server=192.168.1.100,1433;Database=QA;User Id=user;Password=pass;TrustServerCertificate=True
```

## 🔍 Troubleshooting

### Erro: "No default database configured"

**Causa:** Nenhuma variável de ambiente configurada ou argumento passado.

**Solução:**
1. Execute `.\Scripts\setup-env.bat` para configurar variáveis
2. OU adicione `args` no `claude_desktop_config.json`
3. Reinicie o Claude Desktop

### Erro: Transport-level error

**Causa:** Conexão instável com o SQL Server.

**Solução:**
- O servidor v3.1 tem retry automático (até 3 tentativas)
- Verifique se o SQL Server está acessível
- Confira o firewall e permissões de rede

### Banco não está aparecendo

**Verificar variáveis:**
```powershell
Get-ChildItem Env: | Where-Object Name -like "SQL_MCP*"
```

**Verificar logs:**
- Os logs aparecem no stderr do Claude Desktop
- Procure por mensagens como: "Loaded N database(s): ..."

### Claude Desktop não reconhece o servidor

**Checklist:**
1. Caminho do `command` está correto?
2. Arquivo `SqlMcpServer.exe` existe no diretório publish?
3. JSON do `claude_desktop_config.json` está válido? (use um validador JSON)
4. Claude Desktop foi reiniciado após a configuração?

## 📝 Logs e Debugging

### Ver logs do servidor

Os logs são enviados para stderr. No Claude Desktop:
- Mensagens de erro aparecem automaticamente
- Logs de configuração: "Loaded N database(s)"
- Logs de retry: "Transport error on attempt X/4"

### Testar servidor manualmente

```bash
cd publish
.\SqlMcpServer.exe

# Digite mensagens JSON-RPC manualmente:
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
```

## 🎯 Resumo do Processo

```
1. Build
   ├─ .\Scripts\build.bat
   └─ Resultado: .\publish\SqlMcpServer.exe

2. Configurar Variáveis
   ├─ .\Scripts\setup-env.bat
   └─ OU: PowerShell manual

3. Configurar Claude
   ├─ Editar: %APPDATA%\Claude\claude_desktop_config.json
   └─ Adicionar configuração do MCP server

4. Reiniciar
   └─ Fechar e abrir Claude Desktop

5. Testar
   └─ "Claude, liste os bancos de dados disponíveis"
```

## 🆘 Suporte

- **Documentação completa:** README.md
- **Arquivo de exemplo:** Scripts\claude_desktop_config.example.json
- **Guia técnico:** CLAUDE.md
