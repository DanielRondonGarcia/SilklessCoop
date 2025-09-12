// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.HeroAnimatorHook
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using HarmonyLib;
using System;
using UnityEngine;


#nullable enable
namespace SilksongMultiplayer
{
  [HarmonyPatch(typeof (tk2dSpriteAnimator))]
  [HarmonyPatch("Play", new Type[] {typeof (tk2dSpriteAnimationClip), typeof (float), typeof (float)})]
  internal static class HeroAnimatorHook
  {
    private static void Prefix(
      tk2dSpriteAnimationClip clip,
      float clipStartTime,
      float overrideFps,
      tk2dSpriteAnimator __instance)
    {
      if (Object.op_Equality((Object) ((Component) __instance).gameObject, (Object) SilksongMultiplayerAPI.Hero_Hornet))
      {
        int extraValue = -1;
        switch (PlayerData.instance.CurrentCrestID)
        {
          case "Hunter_v3":
            extraValue = 0;
            break;
          case "Reaper":
            extraValue = 1;
            break;
          case "Spell":
            extraValue = 6;
            break;
          case "Toolmaster":
            extraValue = 5;
            break;
          case "Wanderer":
            extraValue = 2;
            break;
          case "Warrior":
            extraValue = 3;
            break;
          case "Witch":
            extraValue = 4;
            break;
        }
        if (extraValue != -1)
        {
          ToolCrest toolCrest = (ToolCrest) null;
          switch (extraValue)
          {
            case 0:
              toolCrest = SilksongMultiplayerAPI.Hunter_v3;
              break;
            case 1:
              toolCrest = SilksongMultiplayerAPI.Reaper;
              break;
            case 2:
              toolCrest = SilksongMultiplayerAPI.Wanderer;
              break;
            case 3:
              toolCrest = SilksongMultiplayerAPI.Warrior;
              break;
            case 4:
              toolCrest = SilksongMultiplayerAPI.Witch;
              break;
            case 5:
              toolCrest = SilksongMultiplayerAPI.Toolmaster;
              break;
            case 6:
              toolCrest = SilksongMultiplayerAPI.Spell;
              break;
          }
          if (Object.op_Inequality((Object) toolCrest, (Object) null))
          {
            if (Object.op_Equality((Object) toolCrest.HeroConfig, (Object) null) || Object.op_Inequality((Object) toolCrest.HeroConfig, (Object) null) && toolCrest.HeroConfig.GetAnimationClip(clip.name) == null)
              extraValue = -1;
          }
          else
            extraValue = -1;
        }
        SilksongMultiplayerAPI.networkDataSender.SendAnimationData(clip.name, extraValue);
      }
      if (!Object.op_Inequality((Object) ((Component) __instance).transform.parent, (Object) null) || !Object.op_Inequality((Object) ((Component) __instance).transform.parent.parent, (Object) null) || !Object.op_Inequality((Object) ((Component) __instance).transform.parent.parent.parent, (Object) null) || !Object.op_Equality((Object) ((Component) ((Component) __instance).transform.parent.parent.parent).gameObject, (Object) SilksongMultiplayerAPI.Hero_Hornet) || !(((Object) ((Component) ((Component) __instance).transform.parent.parent).gameObject).name == "Attacks"))
        return;
      SilksongMultiplayerAPI.networkDataSender.SendHeroAttackAnimationData(((Object) ((Component) __instance).transform.parent).name, ((Object) __instance).name, clip.name);
    }
  }
}
