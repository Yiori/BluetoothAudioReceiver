@echo off
echo.
echo  === Bluetooth Audio Receiver - Instalador ===
echo.

:: Verifica se esta rodando como administrador
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo  [ERRO] Execute este arquivo como Administrador.
    echo  Clique com o botao direito e escolha "Executar como administrador".
    echo.
    pause
    exit /b 1
)

echo  Iniciando instalacao via PowerShell...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-And-Install.ps1"

echo.
pause
