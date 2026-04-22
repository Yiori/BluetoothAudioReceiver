# ============================================================
# Setup-And-Install.ps1
# Instala o app como MSIX usando "loose file registration"
# (Add-AppxPackage -Register) - nao precisa de MakeAppx nem assinatura
# NAO execute diretamente - use: Instalar.bat (como Administrador)
# ============================================================
Set-StrictMode -Off
$ErrorActionPreference = "Stop"

$ProjectDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$CertSubject = "CN=BluetoothAudioReceiver"

# App sera instalado nesta pasta estavel (nao a pasta de Build)
$InstallDir  = Join-Path $env:LOCALAPPDATA "BluetoothAudioReceiver"

Write-Host ""
Write-Host "=== Bluetooth Audio Receiver - Setup ===" -ForegroundColor Cyan
Write-Host ""

# ── 1. Verificar / Ativar Modo Desenvolvedor ───────────────
Write-Host "[1/4] Verificando Modo Desenvolvedor..." -ForegroundColor Green

$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
$devMode = (Get-ItemProperty $regPath -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense

if ($devMode -ne 1) {
    Write-Host "      Ativando automaticamente..." -ForegroundColor Yellow
    reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" /v AllowDevelopmentWithoutDevLicense /t REG_DWORD /d 1 /f | Out-Null
    Write-Host "      Modo Desenvolvedor ativado." -ForegroundColor Green
} else {
    Write-Host "      Ja ativo." -ForegroundColor Gray
}

# ── 2. Publicar o app para pasta de instalacao ─────────────
Write-Host "[2/4] Publicando app em $InstallDir ..." -ForegroundColor Green

# Desregistrar versao anterior antes de substituir arquivos
$existing = Get-AppxPackage -Name "BluetoothAudioReceiver" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "      Removendo registro anterior..." -ForegroundColor Gray
    Remove-AppxPackage -Package $existing.PackageFullName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }

$csproj = Join-Path $ProjectDir "BluetoothAudioReceiver.csproj"
dotnet publish $csproj -c Release -o $InstallDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERRO] dotnet publish falhou." -ForegroundColor Red
    exit 1
}

# Copiar Assets para a pasta de instalacao
$assetsSrc = Join-Path $ProjectDir "Assets"
if (Test-Path $assetsSrc) {
    Copy-Item $assetsSrc (Join-Path $InstallDir "Assets") -Recurse -Force
}

# Criar AppxManifest.xml com Publisher correto
$manifestSrc     = Join-Path $ProjectDir "Package.appxmanifest"
$manifestContent = Get-Content $manifestSrc -Raw
$manifestContent = $manifestContent -replace 'Publisher="[^"]*"', "Publisher=`"$CertSubject`""
Set-Content -Path (Join-Path $InstallDir "AppxManifest.xml") -Value $manifestContent -Encoding UTF8

Write-Host "      Publicado." -ForegroundColor Gray

# ── 3. Criar certificado para o manifesto ──────────────────
Write-Host "[3/4] Verificando certificado..." -ForegroundColor Green

$existing = Get-ChildItem -Path "Cert:\CurrentUser\My" | Where-Object { $_.Subject -eq $CertSubject }
if (-not $existing) {
    $extEku   = "2.5.29.37={text}1.3.6.1.5.5.7.3.3"
    $extBasic = "2.5.29.19={text}"
    $cert = New-SelfSignedCertificate -Type Custom -Subject $CertSubject -KeyUsage DigitalSignature -FriendlyName "BluetoothAudioReceiver Dev Cert" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @($extEku, $extBasic)
    Write-Host "      Certificado criado." -ForegroundColor Gray
} else {
    Write-Host "      Certificado ja existe." -ForegroundColor Gray
}

# ── 4. Registrar app (loose file - sem .msix, sem assinatura) ─
Write-Host "[4/4] Registrando app no Windows..." -ForegroundColor Green

$manifestPath = Join-Path $InstallDir "AppxManifest.xml"

try {
    # -Register instala direto dos arquivos, sem necessidade de empacotar ou assinar
    Add-AppxPackage -Register $manifestPath -ForceUpdateFromAnyVersion

    Write-Host ""
    Write-Host "  SUCESSO! App instalado como pacote com capability 'bluetooth'." -ForegroundColor Green
    Write-Host ""
    Write-Host "  Abra pelo Menu Iniciar: 'Bluetooth Audio Receiver'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  IMPORTANTE: nao mova nem delete a pasta:" -ForegroundColor Yellow
    Write-Host "  $InstallDir" -ForegroundColor Yellow
    Write-Host "  (o app registrado aponta para ela)" -ForegroundColor Yellow
} catch {
    Write-Host ""
    Write-Host "[ERRO] Falha no registro: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Verifique:" -ForegroundColor Yellow
    Write-Host "  1. Modo Desenvolvedor ativo em Configuracoes > Sistema > Para desenvolvedores" -ForegroundColor Yellow
    Write-Host "  2. Feche qualquer instancia do app antes de reinstalar" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
