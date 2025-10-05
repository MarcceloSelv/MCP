@echo off
echo === SQL Validator MCP - Build Script ===
echo.

echo [1/4] Restaurando dependencias...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao restaurar dependencias!
    exit /b 1
)

echo.
echo [2/4] Compilando projeto...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao compilar!
    exit /b 1
)

echo.
echo [3/4] Executando testes...
dotnet test -c Release --no-build
if %ERRORLEVEL% NEQ 0 (
    echo ERRO nos testes!
    exit /b 1
)

echo.
echo [4/4] Publicando aplicacao...
dotnet publish -c Release -o .\publish
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao publicar!
    exit /b 1
)

echo.
echo ========================================
echo BUILD COMPLETO!
echo ========================================
echo.
echo Aplicacao publicada em: .\publish
echo.
echo Para configurar no Claude Desktop:
echo 1. Abra: %%APPDATA%%\Claude\claude_desktop_config.json
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
pause
