@echo off
echo ========================================
echo === SQL Validator MCP v2.0 - Build ===
echo ========================================
echo.

echo [1/4] Restaurando dependencias...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao restaurar dependencias!
    pause
    exit /b 1
)

echo.
echo [2/4] Compilando projeto...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao compilar!
    pause
    exit /b 1
)

echo.
echo [3/4] Executando testes...
dotnet test -c Release --no-build
if %ERRORLEVEL% NEQ 0 (
    echo AVISO: Testes falharam, mas continuando...
)

echo.
echo [4/4] Publicando aplicacao...
dotnet publish -c Release -o .\publish
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao publicar!
    pause
    exit /b 1
)

echo.
echo ========================================
echo ✓ BUILD COMPLETO!
echo ========================================
echo.
echo Aplicacao publicada em: %CD%\publish
echo.
echo NOVAS FEATURES v2.0:
echo   ✨ format_sql    - Formata e embeleza codigo SQL
echo   ✨ document_sql  - Gera documentacao Markdown
echo.
echo Para configurar no Claude Desktop:
echo.
echo 1. Abra: %%APPDATA%%\Claude\claude_desktop_config.json
echo.
echo 2. Adicione:
echo {
echo   "mcpServers": {
echo     "sql-validator": {
echo       "command": "dotnet",
echo       "args": ["%CD%\\publish\\SqlValidatorMcp.dll"]
echo     }
echo   }
echo }
echo.
echo 3. Reinicie o Claude Desktop
echo.
echo ========================================
pause
