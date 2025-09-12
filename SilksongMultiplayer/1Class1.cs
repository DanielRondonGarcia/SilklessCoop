// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.Plugin
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;


#nullable enable
namespace SilksongMultiplayer
{
  [BepInPlugin("com.XvX", "XvX", "1.0.0.0")]
  public class Plugin : BaseUnityPlugin
  {
    internal static ManualLogSource Logger;
    private ConfigEntry<bool> enablePvP;

    private void Awake()
    {
      this.enablePvP = this.Config.Bind<bool>("General", "enablePvP", true, "是否开启pvp");
      SilksongMultiplayerAPI.enablePvP = this.enablePvP.Value;
      Plugin.Logger = this.Logger;
      Plugin.Logger.LogInfo((object) "Plugin XvX is loaded!");
      new Harmony("com.XvX").PatchAll();
      Debug.Log((object) "初始化大厅系统");
      SilksongMultiplayerAPI.RoomManagerObject = Object.Instantiate<GameObject>(new GameObject("LobbyManager"));
      SilksongMultiplayerAPI.RoomManagerObject.AddComponent<RoomManager>();
      SilksongMultiplayerAPI.RoomManagerObject.AddComponent<NetworkDataReceiver>();
      SilksongMultiplayerAPI.RoomManagerObject = GameObject.Find("LobbyManager(Clone)");
      Object.DontDestroyOnLoad((Object) SilksongMultiplayerAPI.RoomManagerObject);
    }
  }
}
