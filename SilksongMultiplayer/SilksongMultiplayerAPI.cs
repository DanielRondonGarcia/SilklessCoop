// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.SilksongMultiplayerAPI
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using Steamworks;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


#nullable enable
namespace SilksongMultiplayer
{
  internal static class SilksongMultiplayerAPI
  {
    public static bool startGame = false;
    public static bool enterRoom = false;
    public static GameObject RoomManagerObject;
    public static RoomManager RoomManager;
    public static PlayerNetworkSync playerNetworkSync;
    public static NetworkDataSender networkDataSender = new NetworkDataSender();
    public static EnemyHitEffectsProfile sampleEnemyHitEffectsProfile;
    public static Font savedFont;
    public static bool enablePvP;
    public static GameObject compassIcon;
    public static GameObject wideCompassIcon;
    public static GameObject Hero_Hornet;
    public static ToolCrest Hunter_v3;
    public static ToolCrest Reaper;
    public static ToolCrest Wanderer;
    public static ToolCrest Warrior;
    public static ToolCrest Witch;
    public static ToolCrest Toolmaster;
    public static ToolCrest Spell;

    public static List<CSteamID> GetRoomMembers() => SilksongMultiplayerAPI.RoomManager.GetRoomMembers();

    public static void SetDamageScalingToCustom(this HealthManager hm)
    {
      Type nestedType = typeof (HealthManager).GetNestedType("DamageScalingConfig", BindingFlags.NonPublic);
      if (nestedType == (Type) null)
      {
        Debug.LogError((object) "找不到 DamageScalingConfig 类型");
      }
      else
      {
        object instance = Activator.CreateInstance(nestedType);
        if (instance == null)
        {
          Debug.LogError((object) "无法创建 DamageScalingConfig 实例");
        }
        else
        {
          FieldInfo field1 = nestedType.GetField("someMultiplierField", BindingFlags.Instance | BindingFlags.NonPublic);
          if (field1 != (FieldInfo) null)
            field1.SetValue(instance, (object) 2f);
          FieldInfo field2 = typeof (HealthManager).GetField("damageScaling", BindingFlags.Instance | BindingFlags.NonPublic);
          if (!(field2 != (FieldInfo) null))
            return;
          field2.SetValue((object) hm, instance);
          Debug.Log((object) "damageScaling 已替换");
        }
      }
    }

    public static void ReplaceItemDropGroups(HealthManager hm)
    {
      if (Object.op_Equality((Object) hm, (Object) null))
      {
        Debug.LogError((object) "HealthManager 实例为空");
      }
      else
      {
        Type nestedType = typeof (HealthManager).GetNestedType("ItemDropGroup", BindingFlags.NonPublic);
        if (nestedType == (Type) null)
        {
          Debug.LogError((object) "找不到 HealthManager.ItemDropGroup 类型");
        }
        else
        {
          object instance = Activator.CreateInstance(typeof (List<>).MakeGenericType(nestedType));
          FieldInfo field = typeof (HealthManager).GetField("itemDropGroups", BindingFlags.Instance | BindingFlags.NonPublic);
          if (field == (FieldInfo) null)
          {
            Debug.LogError((object) "找不到 itemDropGroups 字段");
          }
          else
          {
            field.SetValue((object) hm, instance);
            Debug.Log((object) "成功替换 itemDropGroups 列表");
          }
        }
      }
    }

    public static void CloneAnimatorOfObject(GameObject gameObject, GameObject cloneTarget)
    {
      if (!Object.op_Implicit((Object) cloneTarget.GetComponent<tk2dSpriteAnimator>()) || !Object.op_Implicit((Object) cloneTarget.GetComponent<tk2dSprite>()))
        return;
      gameObject.AddComponent<tk2dSprite>();
      tk2dSpriteAnimator tk2dSpriteAnimator = gameObject.AddComponent<tk2dSpriteAnimator>();
      tk2dSpriteAnimator.Library = cloneTarget.GetComponent<tk2dSpriteAnimator>().Library;
      tk2dSpriteAnimator.Play(cloneTarget.GetComponent<tk2dSpriteAnimator>().CurrentClip);
    }
  }
}
