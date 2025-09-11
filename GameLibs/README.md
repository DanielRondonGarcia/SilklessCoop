# GameLibs - Assemblies Locales

## 📁 ¿Qué es esta carpeta?

Esta carpeta permite usar assemblies del juego de forma local cuando el sistema no puede detectar automáticamente la instalación de Hollow Knight Silksong.

## 🚀 Instrucciones de Uso

### Paso 1: Localizar tu instalación del juego
Busca la carpeta donde tienes instalado Hollow Knight Silksong:

**Steam:**
- `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\`
- `C:\Program Files\Steam\steamapps\common\Hollow Knight Silksong\`

**Epic Games:**
- `C:\Program Files\Epic Games\HollowKnightSilksong\`

**Otras ubicaciones:**
- Revisa tu launcher de juegos para encontrar la ubicación exacta

### Paso 2: Copiar los assemblies necesarios
Copia estos archivos desde `TU_JUEGO\Hollow Knight Silksong_Data\Managed\` a esta carpeta (`GameLibs`):

```
✅ Assembly-CSharp.dll
✅ TeamCherry.TK2D.dll
✅ TeamCherry.Localization.dll
✅ UnityEngine.CoreModule.dll
✅ UnityEngine.UI.dll
```

### Paso 3: Verificar la estructura
Después de copiar, esta carpeta debería verse así:
```
GameLibs/
├── README.md (este archivo)
├── Assembly-CSharp.dll
├── TeamCherry.TK2D.dll
├── TeamCherry.Localization.dll
├── UnityEngine.CoreModule.dll
└── UnityEngine.UI.dll
```

### Paso 4: Compilar
Ejecuta `dotnet build` desde la carpeta raíz del proyecto.

## 🔧 Comandos PowerShell para copiar automáticamente

Si tienes el juego en Steam (ubicación estándar), puedes usar estos comandos:

```powershell
# Para Steam 32-bit
$steamPath = "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong_Data\Managed"
if (Test-Path $steamPath) {
    Copy-Item "$steamPath\Assembly-CSharp.dll" .
    Copy-Item "$steamPath\TeamCherry.TK2D.dll" .
    Copy-Item "$steamPath\TeamCherry.Localization.dll" .
    Copy-Item "$steamPath\UnityEngine.CoreModule.dll" .
    Copy-Item "$steamPath\UnityEngine.UI.dll" .
    Write-Host "✅ Assemblies copiados exitosamente!"
} else {
    Write-Host "❌ No se encontró la instalación de Steam. Verifica la ruta manualmente."
}
```

```powershell
# Para Steam 64-bit
$steamPath = "C:\Program Files\Steam\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong_Data\Managed"
if (Test-Path $steamPath) {
    Copy-Item "$steamPath\Assembly-CSharp.dll" .
    Copy-Item "$steamPath\TeamCherry.TK2D.dll" .
    Copy-Item "$steamPath\TeamCherry.Localization.dll" .
    Copy-Item "$steamPath\UnityEngine.CoreModule.dll" .
    Copy-Item "$steamPath\UnityEngine.UI.dll" .
    Write-Host "✅ Assemblies copiados exitosamente!"
} else {
    Write-Host "❌ No se encontró la instalación de Steam. Verifica la ruta manualmente."
}
```

## ⚠️ Notas Importantes

- **NO** subas estos archivos .dll a Git (ya están en .gitignore)
- Estos archivos son específicos de tu instalación del juego
- Si actualizas el juego, es posible que necesites copiar las versiones nuevas
- Esta es una solución temporal; lo ideal es que el sistema detecte automáticamente tu instalación

## 🆘 ¿Sigues teniendo problemas?

1. Verifica que Hollow Knight Silksong esté instalado
2. Asegúrate de que los archivos .dll existan en la ubicación del juego
3. Ejecuta tu terminal como administrador si hay problemas de permisos
4. Revisa el archivo `QUICK_SETUP.md` en la carpeta raíz para más opciones