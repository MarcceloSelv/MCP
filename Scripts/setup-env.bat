@echo off
echo ========================================
echo SQL MCP Server v3.1 - Configuracao
echo ========================================
echo.

echo Este script ajudara voce a configurar as variaveis de ambiente
echo para o SQL MCP Server.
echo.
echo Escolha o metodo de configuracao:
echo.
echo [1] JSON completo (recomendado para multiplos bancos)
echo [2] Variaveis individuais (mais facil de gerenciar)
echo [3] Mostrar configuracao atual
echo [4] Limpar configuracao
echo [0] Sair
echo.

set /p choice="Digite sua escolha [0-4]: "

if "%choice%"=="0" goto :end
if "%choice%"=="1" goto :json_complete
if "%choice%"=="2" goto :individual
if "%choice%"=="3" goto :show_config
if "%choice%"=="4" goto :clear_config

echo Escolha invalida!
pause
exit /b 1

:json_complete
echo.
echo ========================================
echo Metodo 1: JSON Completo
echo ========================================
echo.
echo Exemplo de JSON:
echo {"qa":"Server=localhost;Database=QA;Integrated Security=true;","prod":"Server=prod-server;Database=MyDB;User Id=user;Password=pass;"}
echo.
echo IMPORTANTE: Use aspas simples (') ao redor do JSON no PowerShell!
echo.

set /p json_databases="Cole seu JSON de bancos de dados: "
if "%json_databases%"=="" (
    echo JSON vazio! Cancelando...
    pause
    exit /b 1
)

echo.
set /p default_db="Digite o nome do banco padrao [opcional]: "

echo.
echo Definindo variaveis de ambiente...
setx SQL_MCP_DATABASES "%json_databases%"

if not "%default_db%"=="" (
    setx SQL_MCP_DEFAULT_DATABASE "%default_db%"
    echo SQL_MCP_DEFAULT_DATABASE = %default_db%
)

echo.
echo ✓ Configuracao salva com sucesso!
echo.
echo IMPORTANTE: Reinicie o Claude Desktop para aplicar as mudancas.
echo.
pause
goto :end

:individual
echo.
echo ========================================
echo Metodo 2: Variaveis Individuais
echo ========================================
echo.
echo Vamos configurar os bancos de dados um por um.
echo Deixe em branco para finalizar.
echo.

set count=0

:ask_database
set /p db_name="Nome do banco (ex: QA, PROD) [Enter para finalizar]: "
if "%db_name%"=="" goto :finish_individual

set /p conn_string="Connection String para %db_name%: "
if "%conn_string%"=="" (
    echo Connection string vazia! Pulando...
    goto :ask_database
)

echo Definindo SQL_MCP_DB_%db_name%...
setx SQL_MCP_DB_%db_name% "%conn_string%"

set /a count+=1
echo ✓ Banco '%db_name%' configurado!
echo.
goto :ask_database

:finish_individual
if %count%==0 (
    echo Nenhum banco configurado!
    pause
    exit /b 1
)

echo.
set /p default_db="Digite o nome do banco padrao: "
if not "%default_db%"=="" (
    setx SQL_MCP_DEFAULT_DATABASE "%default_db%"
    echo SQL_MCP_DEFAULT_DATABASE = %default_db%
)

echo.
echo ✓ %count% banco(s) configurado(s) com sucesso!
echo.
echo IMPORTANTE: Reinicie o Claude Desktop para aplicar as mudancas.
echo.
pause
goto :end

:show_config
echo.
echo ========================================
echo Configuracao Atual
echo ========================================
echo.

echo SQL_MCP_DATABASES:
if defined SQL_MCP_DATABASES (
    echo   %SQL_MCP_DATABASES%
) else (
    echo   [nao configurado]
)
echo.

echo SQL_MCP_DEFAULT_DATABASE:
if defined SQL_MCP_DEFAULT_DATABASE (
    echo   %SQL_MCP_DEFAULT_DATABASE%
) else (
    echo   [nao configurado]
)
echo.

echo Variaveis individuais (SQL_MCP_DB_*):
set SQL_MCP_DB_ 2>nul
if %errorlevel% neq 0 (
    echo   [nenhuma configurada]
)

echo.
pause
goto :end

:clear_config
echo.
echo ========================================
echo Limpar Configuracao
echo ========================================
echo.
echo ATENCAO: Isso removera TODAS as variaveis de ambiente do SQL MCP Server!
echo.
set /p confirm="Tem certeza? (S/N): "

if /i not "%confirm%"=="S" (
    echo Cancelado.
    pause
    goto :end
)

echo.
echo Removendo variaveis...

reg delete "HKCU\Environment" /v SQL_MCP_DATABASES /f 2>nul
reg delete "HKCU\Environment" /v SQL_MCP_DEFAULT_DATABASE /f 2>nul

for /f "tokens=1* delims==" %%a in ('set SQL_MCP_DB_ 2^>nul') do (
    reg delete "HKCU\Environment" /v %%a /f 2>nul
    echo Removido: %%a
)

echo.
echo ✓ Configuracao removida!
echo.
echo IMPORTANTE: Reinicie o Claude Desktop para aplicar as mudancas.
echo.
pause
goto :end

:end
echo.
echo Configuracao concluida!
echo.
echo Proximo passo: Configure o claude_desktop_config.json
echo Veja: .\Scripts\claude_desktop_config.example.json
echo.
pause
