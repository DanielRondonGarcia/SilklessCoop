# Script para instalar SilklessCoop mod automaticamente
# Descarga BepInEx, lo instala y copia el mod

param(
    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong"
)

Write-Host "=== Instalador automatico de SilklessCoop ===" -ForegroundColor Cyan

# Verificar que existe la ruta del juego
if (-not (Test-Path $GamePath)) {
    Write-Host "Error: No se encuentra el juego en '$GamePath'" -ForegroundColor Red
    Write-Host "Especifica la ruta correcta:" -ForegroundColor Yellow
    Write-Host ".\install-mod.ps1 'RUTA_DEL_JUEGO'" -ForegroundColor White
    exit 1
}

Write-Host "Juego encontrado en: $GamePath" -ForegroundColor Green

# URLs y rutas
$bepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x64_5.4.23.3.zip"
$tempDir = "$env:TEMP\SilklessCoopInstaller"
$bepInExZip = "$tempDir\BepInEx.zip"
$modDll = "bin\Debug\netstandard2.1\SilklessCoop.dll"

# Crear directorio temporal
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Write-Host "\nDescargando BepInEx..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $bepInExUrl -OutFile $bepInExZip -UseBasicParsing
    Write-Host "BepInEx descargado exitosamente." -ForegroundColor Green
} catch {
    Write-Host "Error al descargar BepInEx: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "\nExtrayendo BepInEx en el juego..." -ForegroundColor Yellow
try {
    Expand-Archive -Path $bepInExZip -DestinationPath $GamePath -Force
    Write-Host "BepInEx instalado exitosamente." -ForegroundColor Green
} catch {
    Write-Host "Error al extraer BepInEx: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Verificar que el mod esta compilado
if (-not (Test-Path $modDll)) {
    Write-Host "\nEl mod no esta compilado. Compilando..." -ForegroundColor Yellow
    try {
        dotnet build
        if ($LASTEXITCODE -ne 0) {
            throw "Error en la compilacion"
        }
        Write-Host "Mod compilado exitosamente." -ForegroundColor Green
    } catch {
        Write-Host "Error al compilar el mod: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Ejecuta 'dotnet build' manualmente para ver los errores." -ForegroundColor Yellow
        exit 1
    }
}

# Crear carpeta plugins si no existe
$pluginsDir = "$GamePath\BepInEx\plugins"
if (-not (Test-Path $pluginsDir)) {
    New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null
    Write-Host "Carpeta plugins creada." -ForegroundColor Green
}

# Copiar el mod
Write-Host "\nCopiando el mod..." -ForegroundColor Yellow
try {
    Copy-Item $modDll "$pluginsDir\SilklessCoop.dll" -Force
    Write-Host "Mod copiado exitosamente." -ForegroundColor Green
} catch {
    Write-Host "Error al copiar el mod: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Limpiar archivos temporales
Remove-Item $tempDir -Recurse -Force

Write-Host "\n=== INSTALACION COMPLETADA ===" -ForegroundColor Cyan
Write-Host "✓ BepInEx instalado" -ForegroundColor Green
Write-Host "✓ SilklessCoop mod instalado" -ForegroundColor Green
Write-Host "\nPasos siguientes:" -ForegroundColor Yellow
Write-Host "1. Ejecuta Hollow Knight Silksong una vez para configurar BepInEx" -ForegroundColor White
Write-Host "2. Busca el boton 'Multiplayer' en el menu principal" -ForegroundColor White
Write-Host "3. ¡Disfruta el modo cooperativo!" -ForegroundColor White
Write-Host "\nSi hay problemas, revisa los logs en:" -ForegroundColor Yellow
Write-Host "$GamePath\BepInEx\LogOutput.log" -ForegroundColor White