// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.RestoreLanguagePatch
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using HarmonyLib;
using UnityEngine;

namespace SilksongMultiplayer
{
  [HarmonyPatch(typeof (StartManager), "SwitchToMenuScene")]
  internal static class RestoreLanguagePatch
  {
    private static void Prefix()
    {
      Debug.Log((object) "[Harmony] RestoreLanguageSelection 被调用");
      SilksongMultiplayerAPI.RoomManagerObject = Object.Instantiate<GameObject>(new GameObject("LobbyManager"));
      SilksongMultiplayerAPI.RoomManagerObject.AddComponent<RoomManager>();
      SilksongMultiplayerAPI.RoomManagerObject.AddComponent<NetworkDataReceiver>();
      SilksongMultiplayerAPI.RoomManagerObject = GameObject.Find("LobbyManager(Clone)");
      Object.DontDestroyOnLoad((Object) SilksongMultiplayerAPI.RoomManagerObject);
    }
  }
}
