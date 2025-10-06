@echo off
echo === SQL Validator Test Console ===
echo.

cd TestConsole

echo [1/3] Restaurando dependencias...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao restaurar dependencias!
    pause
    exit /b 1
)

echo.
echo [2/3] Compilando projeto...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERRO ao compilar!
    pause
    exit /b 1
)

echo.
echo [3/3] Executando testes...
echo.
dotnet run -c Release

echo.
echo ========================================
pause
