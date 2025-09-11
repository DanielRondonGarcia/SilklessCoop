# Script rapido para actualizar solo el mod SilklessCoop
# (Para usuarios que ya tienen BepInEx instalado)

param(
    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong"
)

Write-Host "=== Actualizador rapido de SilklessCoop ===" -ForegroundColor Cyan

# Verificar que existe BepInEx
$pluginsDir = "$GamePath\BepInEx\plugins"
if (-not (Test-Path $pluginsDir)) {
    Write-Host "Error: BepInEx no esta instalado en '$GamePath'" -ForegroundColor Red
    Write-Host "Usa 'install-mod.ps1' para la instalacion completa." -ForegroundColor Yellow
    exit 1
}

# Verificar que el mod esta compilado
$modDll = "bin\Debug\netstandard2.1\SilklessCoop.dll"
if (-not (Test-Path $modDll)) {
    Write-Host "Compilando el mod..." -ForegroundColor Yellow
    dotnet build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error al compilar. Revisa los errores arriba." -ForegroundColor Red
        exit 1
    }
}

# Copiar el mod
Write-Host "Actualizando mod..." -ForegroundColor Yellow
Copy-Item $modDll "$pluginsDir\SilklessCoop.dll" -Force

Write-Host "Mod actualizado exitosamente!" -ForegroundColor Green
Write-Host "Reinicia el juego para ver los cambios." -ForegroundColor White