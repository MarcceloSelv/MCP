@echo off
echo ========================================
echo === SQL MCP Server - Teste Local ===
echo ========================================
echo.

set DATABASES={\"QA\":\"Server=10.0.0.181;Database=MRC_QA;User Id=sistema;Password=SYSUSER;TrustServerCertificate=True;Application Name=SqlMcpServer\",\"Norcoast\":\"Server=10.0.51.11;Database=ECARGO;User Id=SISTEMA;Password=SYSUSER;TrustServerCertificate=True;Application Name=SqlMcpServer\",\"Cargosol\":\"Server=10.0.0.181;Database=Cargosol;User Id=SISTEMA;Password=SYSUSER;TrustServerCertificate=True;Application Name=SqlMcpServer\"}

echo Iniciando SQL MCP Server...
echo.
echo Default Database: QA
echo Available Databases: QA, Norcoast, Cargosol
echo.
echo O servidor aguardara entrada JSON-RPC via stdin...
echo.
echo Exemplos de comandos JSON-RPC:
echo.
echo 1. Initialize:
echo {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
echo.
echo 2. List Databases:
echo {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_databases","arguments":{}}}
echo.
echo 3. Validate SQL:
echo {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"validate_sql","arguments":{"query":"SELECT TOP 10 * FROM Users"}}}
echo.
echo Pressione Ctrl+C para sair
echo.
echo ========================================
echo.

publish\SqlMcpServer.exe --databases=%DATABASES% --default-database=QA
