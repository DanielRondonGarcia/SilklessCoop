# Script para verificar objetos de menú en Hollow Knight Silksong
# Ejecutar después de iniciar el juego

Write-Host "=== Debug de objetos de menú en Hollow Knight Silksong ===" -ForegroundColor Cyan

$gamePath = "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong"
$bepinexLogPath = "$gamePath\BepInEx\LogOutput.log"
$playerLogPath = "$env:USERPROFILE\AppData\LocalLow\Team Cherry\Hollow Knight Silksong\Player.log"

Write-Host "Verificando logs..." -ForegroundColor Yellow

if (Test-Path $bepinexLogPath) {
    Write-Host "✓ Encontrado BepInEx LogOutput.log" -ForegroundColor Green
    
    # Buscar líneas relacionadas con SilklessCoop
    $silklessLines = Get-Content $bepinexLogPath | Where-Object { $_ -match "SilklessCoop" }
    if ($silklessLines) {
        Write-Host "`nLíneas de SilklessCoop en el log:" -ForegroundColor Cyan
        $silklessLines | ForEach-Object { Write-Host "  $_" }
    }
    
    # Buscar líneas relacionadas con menú/UI
    $menuLines = Get-Content $bepinexLogPath | Where-Object { $_ -match "menu|screen|button|MainMenu|Found|Child" -or $_ -match "menu" -or $_ -match "screen" -or $_ -match "button" }
    if ($menuLines) {
        Write-Host "`nLíneas relacionadas con menú/UI:" -ForegroundColor Cyan
        $menuLines | ForEach-Object { Write-Host "  $_" }
    }
    
    # Mostrar errores
    $errorLines = Get-Content $bepinexLogPath | Where-Object { $_ -match "Error" -or $_ -match "Exception" -or $_ -match "Failed" }
    if ($errorLines) {
        Write-Host "`nErrores encontrados:" -ForegroundColor Red
        $errorLines | ForEach-Object { Write-Host "  $_" }
    }
    
    # Mostrar las últimas 20 líneas
    Write-Host "`nÚltimas 20 líneas del log:" -ForegroundColor Yellow
    $lastLines = Get-Content $bepinexLogPath -Tail 20
    $lastLines | ForEach-Object { Write-Host "  $_" }
    
} else {
    Write-Host "✗ No se encontró BepInEx LogOutput.log en: $bepinexLogPath" -ForegroundColor Red
}

Write-Host "`n" + "="*60 + "`n"

if (Test-Path $playerLogPath) {
    Write-Host "✓ Encontrado Player.log de Unity" -ForegroundColor Green
    $unityErrors = Get-Content $playerLogPath | Where-Object { $_ -match "Error" -or $_ -match "Exception" -or $_ -match "Failed" }
    if ($unityErrors) {
        Write-Host "`nErrores de Unity:" -ForegroundColor Red
        $unityErrors | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }
    }
} else {
    Write-Host "✗ No se encontró Player.log en: $playerLogPath" -ForegroundColor Red
}

Write-Host "`n=== Instrucciones ===" -ForegroundColor Cyan
Write-Host "1. Ejecuta Hollow Knight Silksong"
Write-Host "2. Ve al menú principal"
Write-Host "3. Ejecuta este script nuevamente para ver los logs actualizados"
Write-Host "4. Busca líneas que mencionen objetos de menú encontrados o no encontrados"

Write-Host "`nSi el mod no aparece en el menú, revisa si hay errores en los logs arriba." -ForegroundColor Yellow