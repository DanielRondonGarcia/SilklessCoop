// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.PatchClass
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using UnityEngine;

namespace SilksongMultiplayer
{
  public static class PatchClass
  {
    private static void Prefix() => Debug.Log((object) "目标方法被调用了！");
  }
}
