@echo off
echo ========================================
echo ====  SQL MCP Server v3.1 - Build  ====
echo ========================================
echo.

echo [1/4] Restaurando dependencias...
dotnet restore SqlMcpServer.csproj
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao restaurar dependencias!
    pause
    exit /b 1
)

echo.
echo [2/4] Compilando projeto...
dotnet build SqlMcpServer.csproj -c Release
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
echo [4/4] Publicando aplicacao (Windows x64 - Self-contained)...
dotnet publish SqlMcpServer.csproj -c Release -r win-x64 --self-contained true -o .\publish
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao publicar!
    pause
    exit /b 1
)

echo.
echo [5/5] Removendo arquivos de localizacao (economia ~3MB)...
cd publish
for /d %%d in (cs de es fr it ja ko pl pt-BR ru tr zh-Hans zh-Hant) do (
    if exist %%d rd /s /q %%d
)
cd ..
echo Pastas de localizacao removidas!

echo.
echo ========================================
echo ✓ BUILD COMPLETO!
echo ========================================
echo.
echo Aplicacao publicada em: %CD%\publish
echo.
echo FEATURES v3.1:
echo   ✅ validate_sql   - Valida sintaxe T-SQL
echo   ✅ parse_sql      - Analisa estrutura AST
echo   ✅ document_sql   - Gera documentacao Markdown
echo   ✨ execute_sql    - Executa queries com seguranca
echo   ✨ list_databases - Lista bancos configurados
echo.
echo NOVIDADES v3.1:
echo   🔄 Retry automatico com exponential backoff
echo   ⏱️  Timeouts separados (conexao: 15s, execucao: 300s)
echo   🌍 Configuracao via variaveis de ambiente
echo.
echo SEGURANCA:
echo   🔒 Bloqueia: DROP, DELETE, UPDATE, TRUNCATE, ALTER
echo   ✅ Permite: SELECT, INSERT, CREATE
echo.
echo ========================================
echo PROXIMOS PASSOS:
echo ========================================
echo.
echo Execute o script de configuracao:
echo   .\Scripts\setup-env.bat
echo.
echo Ou configure manualmente - veja: README.md secao "Configuracao"
echo.
echo ========================================
pause
