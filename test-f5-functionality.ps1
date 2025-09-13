# Script para probar la funcionalidad de la tecla F5 en SilklessCoop

Write-Host "=== Prueba de Funcionalidad F5 - SilklessCoop ===" -ForegroundColor Cyan
Write-Host ""

# Verificar que el mod esté instalado
$modPath = "D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\SilklessCoop.dll"
if (Test-Path $modPath) {
    $modInfo = Get-Item $modPath
    Write-Host "✓ Mod instalado correctamente" -ForegroundColor Green
    Write-Host "  Ubicación: $modPath" -ForegroundColor Gray
    Write-Host "  Tamaño: $([math]::Round($modInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
    Write-Host "  Última modificación: $($modInfo.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "✗ Mod NO encontrado en $modPath" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== INSTRUCCIONES PARA PROBAR ===" -ForegroundColor Yellow
Write-Host "1. Ejecuta Hollow Knight Silksong" -ForegroundColor White
Write-Host "2. Una vez en el juego (en cualquier escena), presiona F5" -ForegroundColor White
Write-Host "3. Deberías ver un mensaje en pantalla que dice:" -ForegroundColor White
Write-Host "   'Modo Multijugador ACTIVADO' (en verde)" -ForegroundColor Green
Write-Host "4. Presiona F5 nuevamente para desactivar" -ForegroundColor White
Write-Host "5. Deberías ver:" -ForegroundColor White
Write-Host "   'Modo Multijugador DESACTIVADO' (en rojo)" -ForegroundColor Red
Write-Host ""
Write-Host "Nota: Los mensajes aparecen en la parte superior central de la pantalla" -ForegroundColor Cyan
Write-Host "      y desaparecen automáticamente después de 3 segundos" -ForegroundColor Cyan
Write-Host ""
Write-Host "=== VERIFICACIÓN DE LOGS ===" -ForegroundColor Yellow
Write-Host "Busca estas líneas en los logs cuando presiones F5:" -ForegroundColor White
Write-Host "- '[SilklessCoop] Modo multijugador ACTIVADO! Presiona F5 para desactivar.'" -ForegroundColor Green
Write-Host "- '[SilklessCoop] Modo multijugador DESACTIVADO.'" -ForegroundColor Red
Write-Host "- 'Inicializando sistema de red...'" -ForegroundColor Gray
Write-Host "- 'Cerrando sistema de red...'" -ForegroundColor Gray