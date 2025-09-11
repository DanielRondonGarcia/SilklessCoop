# Script para copiar las DLLs del juego
# Uso: .\copy-game-assemblies.ps1 "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\Silksong_Data\Managed"

param(
    [string]$GamePath = ""
)

Write-Host "=== Copiador de DLLs de Hollow Knight Silksong ===" -ForegroundColor Cyan

if ($GamePath -eq "") {
    Write-Host "Buscando Hollow Knight Silksong..." -ForegroundColor Yellow
    
    # Rutas comunes del juego
    $gamePaths = @(
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong_Data\Managed",
        "${env:ProgramFiles}\Steam\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong_Data\Managed",
        "${env:ProgramFiles}\Epic Games\HollowKnightSilksong\Hollow Knight Silksong_Data\Managed",
        "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong_Data\Managed"
    )
    
    $gameFound = $false
    foreach ($path in $gamePaths) {
        if (Test-Path $path) {
            $GamePath = $path
            $gameFound = $true
            Write-Host "Juego encontrado en: $path" -ForegroundColor Green
            break
        }
    }
    
    if (-not $gameFound) {
        Write-Host "No se encontro el juego automaticamente." -ForegroundColor Red
        Write-Host "Ejecuta el script asi:" -ForegroundColor Yellow
        Write-Host ".\copy-game-assemblies.ps1 'RUTA_COMPLETA_A_MANAGED'" -ForegroundColor White
        Write-Host "Ejemplo:" -ForegroundColor Yellow
        Write-Host ".\copy-game-assemblies.ps1 'D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\Silksong_Data\Managed'" -ForegroundColor White
        exit 1
    }
} else {
    Write-Host "Usando ruta especificada: $GamePath" -ForegroundColor Green
}

# Verificar que la ruta existe
if (-not (Test-Path $GamePath)) {
    Write-Host "Error: La ruta '$GamePath' no existe." -ForegroundColor Red
    exit 1
}

# Crear carpeta GameLibs
if (-not (Test-Path "GameLibs")) {
    New-Item -ItemType Directory -Path "GameLibs" -Force | Out-Null
    Write-Host "Carpeta GameLibs creada." -ForegroundColor Green
}

# DLLs necesarias
$dlls = @(
    "Assembly-CSharp.dll",
    "TeamCherry.TK2D.dll",
    "TeamCherry.Localization.dll",
    "TeamCherry.BuildBot.dll",
    "TeamCherry.Cinematics.dll",
    "TeamCherry.NestedFadeGroup.dll",
    "TeamCherry.SharedUtils.dll",
    "TeamCherry.Splines.dll",
    "UnityEngine.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.LocalizationModule.dll"
)

Write-Host "`nCopiando DLLs desde: $GamePath" -ForegroundColor Yellow

$copied = 0
$missing = @()

foreach ($dll in $dlls) {
    $source = Join-Path $GamePath $dll
    $dest = Join-Path "GameLibs" $dll
    
    if (Test-Path $source) {
        Copy-Item $source $dest -Force
        Write-Host "Copiado: $dll" -ForegroundColor Green
        $copied++
    } else {
        Write-Host "No encontrado: $dll" -ForegroundColor Red
        $missing += $dll
    }
}

Write-Host "`n=== RESUMEN ===" -ForegroundColor Cyan
Write-Host "DLLs copiadas: $copied de $($dlls.Count)" -ForegroundColor Green

if ($missing.Count -gt 0) {
    Write-Host "DLLs faltantes: $($missing -join ', ')" -ForegroundColor Red
}

if ($copied -gt 0) {
    Write-Host "`nListo! Ahora ejecuta: dotnet build" -ForegroundColor Yellow
} else {
    Write-Host "`nNo se copiaron DLLs. Verifica la ruta." -ForegroundColor Red
}