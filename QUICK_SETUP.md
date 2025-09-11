# Quick Setup Guide - Fix Build Errors

## 🚀 Quick Fix (2 minutes)

### Step 1: Update Game Path
Edit the `SilklessCoop.csproj` file and update this line with your actual game installation path:

```xml
<SILKSONG_GAME_PATH Condition="'$(SILKSONG_GAME_PATH)' == ''">YOUR_GAME_PATH_HERE\</SILKSONG_GAME_PATH>
```

**Common paths:**
- Steam: `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\`
- Epic Games: `C:\Program Files\Epic Games\HollowKnightSilksong\`

### Step 2: Verify Game Files Exist
Check that these files exist in your game installation:
```
YOUR_GAME_PATH\Hollow Knight Silksong_Data\Managed\
├── TeamCherry.TK2D.dll
├── TeamCherry.Localization.dll
├── Assembly-CSharp.dll
├── UnityEngine.CoreModule.dll
└── UnityEngine.UI.dll
```

### Step 3: Build
```bash
dotnet build
```

## 🔧 Alternative: Use Environment Variable

Set a system environment variable:
- Variable: `SILKSONG_GAME_PATH`
- Value: `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\`

Then restart your terminal and run `dotnet build`.

## ⚠️ Still Having Issues?

1. **Game not installed?** - You need Hollow Knight Silksong installed
2. **Different path?** - Check your game launcher for the installation location
3. **Missing DLLs?** - Verify game files through your game launcher
4. **Permission errors?** - Run terminal as administrator

## 📝 What Was Fixed

- ✅ Updated assembly reference paths to use configurable game path
- ✅ Added Steamworks.NET NuGet package as alternative to game DLL
- ✅ Improved path handling with MSBuild properties
- ✅ Maintained compatibility with existing code

The project now uses `$(SILKSONG_GAME_PATH)` variable instead of hardcoded relative paths, making it much easier to configure for different installations.