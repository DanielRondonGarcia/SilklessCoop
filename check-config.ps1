# Script para verificar si se crea el archivo de configuración SilklessCoop.cfg
# Ejecutar después de iniciar el juego una vez

Write-Host "=== Verificador de configuración SilklessCoop ===" -ForegroundColor Cyan

$configPath = "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\config\SilklessCoop.cfg"
$pluginPath = "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\SilklessCoop.dll"
$logPath = "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\LogOutput.log"

Write-Host "Verificando archivos..." -ForegroundColor Yellow

# Verificar que el plugin esté instalado
if (Test-Path $pluginPath) {
    $pluginInfo = Get-Item $pluginPath
    Write-Host "✓ Plugin instalado: $($pluginInfo.Name)" -ForegroundColor Green
    Write-Host "  Tamaño: $($pluginInfo.Length) bytes" -ForegroundColor Gray
    Write-Host "  Modificado: $($pluginInfo.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "✗ Plugin NO encontrado en: $pluginPath" -ForegroundColor Red
    exit 1
}

# Verificar si existe el archivo de configuración
if (Test-Path $configPath) {
    Write-Host "✓ Archivo de configuración encontrado!" -ForegroundColor Green
    Write-Host "\nContenido del archivo SilklessCoop.cfg:" -ForegroundColor Cyan
    Write-Host "=" * 50 -ForegroundColor Gray
    Get-Content $configPath | ForEach-Object { Write-Host "  $_" }
    Write-Host "=" * 50 -ForegroundColor Gray
} else {
    Write-Host "✗ Archivo de configuración NO encontrado" -ForegroundColor Red
    Write-Host "  Ruta esperada: $configPath" -ForegroundColor Yellow
    Write-Host "\n¿Has ejecutado el juego al menos una vez después de instalar el mod?" -ForegroundColor Yellow
}

# Verificar logs recientes
if (Test-Path $logPath) {
    Write-Host "\n=== Logs recientes de SilklessCoop ===" -ForegroundColor Cyan
    $recentLogs = Get-Content $logPath | Where-Object { $_ -match "SilklessCoop" } | Select-Object -Last 10
    if ($recentLogs) {
        $recentLogs | ForEach-Object { Write-Host "  $_" -ForegroundColor White }
    } else {
        Write-Host "  No se encontraron logs de SilklessCoop" -ForegroundColor Yellow
    }
    
    # Verificar errores de configuración
    $configErrors = Get-Content $logPath | Where-Object { $_ -match "SilklessCoop" -and ($_ -match "Error" -or $_ -match "Exception" -or $_ -match "Failed") } | Select-Object -Last 5
    if ($configErrors) {
        Write-Host "\n=== Errores relacionados con SilklessCoop ===" -ForegroundColor Red
        $configErrors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    }
} else {
    Write-Host "\n✗ No se encontró el archivo de log de BepInEx" -ForegroundColor Red
}

Write-Host "\n=== Instrucciones ===" -ForegroundColor Cyan
Write-Host "1. Si el archivo .cfg NO existe:" -ForegroundColor Yellow
Write-Host "   - Ejecuta Hollow Knight Silksong una vez" -ForegroundColor White
Write-Host "   - Espera a que cargue completamente el menú principal" -ForegroundColor White
Write-Host "   - Cierra el juego y ejecuta este script nuevamente" -ForegroundColor White
Write-Host "\n2. Si el archivo .cfg existe:" -ForegroundColor Yellow
Write-Host "   - ¡El mod está funcionando correctamente!" -ForegroundColor Green
Write-Host "   - Busca el botón 'Coop' en el menú principal del juego" -ForegroundColor White
Write-Host "\n3. Si hay errores en los logs:" -ForegroundColor Yellow
Write-Host "   - Revisa los mensajes de error arriba" -ForegroundColor White
Write-Host "   - Puede que falten dependencias o haya problemas de compatibilidad" -ForegroundColor White