// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.NetworkDataReceiver
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using GlobalEnums;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


#nullable enable
namespace SilksongMultiplayer
{
  internal class NetworkDataReceiver : MonoBehaviour
  {
    private Dictionary<CSteamID, PlayerAvatar> remotePlayers = new Dictionary<CSteamID, PlayerAvatar>();

    private void Update()
    {
      uint length;
      while (SteamNetworking.IsP2PPacketAvailable(ref length, 0))
      {
        byte[] data = new byte[(int) length];
        uint num;
        CSteamID senderID;
        if (SteamNetworking.ReadP2PPacket(data, length, ref num, ref senderID, 0))
          this.ProcessPacket(data, senderID);
      }
    }

    private void ProcessPacket(byte[] data, CSteamID senderID)
    {
      if (!SilksongMultiplayerAPI.startGame)
        return;
      switch (data[0])
      {
        case 1:
          this.HandlePositionMessage(data, senderID);
          break;
        case 2:
          this.HandleAnimationMessage(data, senderID);
          break;
        case 3:
          this.HandleMapChange(data, senderID);
          break;
        case 4:
          this.HandlePlayerTakeDamageMessage(data, senderID);
          break;
        case 5:
          this.HandleMapPositionMessage(data, senderID);
          break;
        case 6:
          this.HandleHeroAttackAnimation(data, senderID);
          break;
      }
    }

    private void HandlePositionMessage(byte[] data, CSteamID senderID)
    {
      float single1 = BitConverter.ToSingle(data, 1);
      float single2 = BitConverter.ToSingle(data, 5);
      float single3 = BitConverter.ToSingle(data, 9);
      float single4 = BitConverter.ToSingle(data, 13);
      Vector3 vector3 = new Vector3(single1, single2, single3);
      PlayerAvatar playerAvatar;
      if (this.remotePlayers.TryGetValue(senderID, out playerAvatar))
        playerAvatar.UpdatePosition(vector3, single4);
      else if (SilksongMultiplayerAPI.startGame)
        this.CreateNewPlayer(senderID, vector3);
    }

    private void HandleAnimationMessage(byte[] data, CSteamID senderID)
    {
      int num = 1;
      byte[] numArray = data;
      int index1 = num;
      int index2 = index1 + 1;
      int count = (int) numArray[index1];
      string str = Encoding.UTF8.GetString(data, index2, count);
      int startIndex = index2 + count;
      int int32 = BitConverter.ToInt32(data, startIndex);
      PlayerAvatar playerAvatar;
      if (!this.remotePlayers.TryGetValue(senderID, out playerAvatar))
        return;
      Debug.Log((object) ("动作ID：" + str));
      ToolCrest toolCrest = (ToolCrest) null;
      switch (int32)
      {
        case -1:
          ((Component) playerAvatar).GetComponent<tk2dSpriteAnimator>().Play(str);
          break;
        case 0:
          toolCrest = SilksongMultiplayerAPI.Hunter_v3;
          goto default;
        case 1:
          toolCrest = SilksongMultiplayerAPI.Reaper;
          goto default;
        case 2:
          toolCrest = SilksongMultiplayerAPI.Wanderer;
          goto default;
        case 3:
          toolCrest = SilksongMultiplayerAPI.Warrior;
          goto default;
        case 4:
          toolCrest = SilksongMultiplayerAPI.Witch;
          goto default;
        case 5:
          toolCrest = SilksongMultiplayerAPI.Toolmaster;
          goto default;
        case 6:
          toolCrest = SilksongMultiplayerAPI.Spell;
          goto default;
        default:
          ((Component) playerAvatar).GetComponent<tk2dSpriteAnimator>().Play(toolCrest.HeroConfig.GetAnimationClip(str));
          break;
      }
    }

    private void HandleMapChange(byte[] data, CSteamID senderID)
    {
      byte count = data[1];
      string mapName_get = Encoding.UTF8.GetString(data, 2, (int) count);
      Debug.Log((object) string.Format("收到地图切换: {0} (来自 {1})", (object) mapName_get, (object) senderID));
      PlayerAvatar playerAvatar;
      if (!this.remotePlayers.TryGetValue(senderID, out playerAvatar))
        return;
      playerAvatar.UpdateMap(mapName_get);
    }

    private void HandlePlayerTakeDamageMessage(byte[] data, CSteamID senderID)
    {
      int startIndex1 = 1;
      ulong uint64 = BitConverter.ToUInt64(data, startIndex1);
      int startIndex2 = startIndex1 + 8;
      int int32_1 = BitConverter.ToInt32(data, startIndex2);
      int startIndex3 = startIndex2 + 4;
      int int32_2 = BitConverter.ToInt32(data, startIndex3);
      int startIndex4 = startIndex3 + 4;
      int int32_3 = BitConverter.ToInt32(data, startIndex4);
      int num = startIndex4 + 4;
      int int32_4 = BitConverter.ToInt32(data, 21);
      if (SilksongMultiplayerAPI.enablePvP)
      {
        Debug.Log((object) (uint64.ToString() + " 和 " + SteamUser.GetSteamID().m_SteamID.ToString()));
        if ((long) uint64 == (long) SteamUser.GetSteamID().m_SteamID)
          HeroController.instance.TakeDamage(((Component) this).gameObject, (CollisionSide) int32_2, int32_1, (HazardType) int32_3, (DamagePropertyFlags) 0);
      }
      PlayerAvatar playerAvatar;
      if (!this.remotePlayers.TryGetValue(senderID, out playerAvatar))
        return;
      playerAvatar.HitEffect((CollisionSide) int32_2, int32_1, (AttackTypes) int32_4);
    }

    private void HandleMapPositionMessage(byte[] data, CSteamID senderID)
    {
      float single1 = BitConverter.ToSingle(data, 1);
      float single2 = BitConverter.ToSingle(data, 5);
      float single3 = BitConverter.ToSingle(data, 9);
      float single4 = BitConverter.ToSingle(data, 13);
      Vector2 compass = new Vector2(single1, single2);
      Vector2 wideCompass = new Vector2(single3, single4);
      PlayerAvatar playerAvatar;
      if (!this.remotePlayers.TryGetValue(senderID, out playerAvatar))
        return;
      playerAvatar.UpdateCompassPosition(compass, wideCompass);
    }

    private void HandleHeroAttackAnimation(byte[] data, CSteamID senderID)
    {
      int num1 = 1;
      byte[] numArray1 = data;
      int index1 = num1;
      int index2 = index1 + 1;
      int count1 = (int) numArray1[index1];
      string str1 = Encoding.UTF8.GetString(data, index2, count1);
      int num2 = index2 + count1;
      byte[] numArray2 = data;
      int index3 = num2;
      int index4 = index3 + 1;
      int count2 = (int) numArray2[index3];
      string str2 = Encoding.UTF8.GetString(data, index4, count2);
      int num3 = index4 + count2;
      byte[] numArray3 = data;
      int index5 = num3;
      int index6 = index5 + 1;
      int count3 = (int) numArray3[index5];
      string str3 = Encoding.UTF8.GetString(data, index6, count3);
      int num4 = index6 + count3;
      PlayerAvatar playerAvatar;
      if (!this.remotePlayers.TryGetValue(senderID, out playerAvatar) || !Object.op_Implicit((Object) ((Component) playerAvatar).transform.Find("Attacks(Clone)")) || !Object.op_Implicit((Object) ((Component) playerAvatar).transform.Find("Attacks(Clone)").Find(str1 + "(Clone)")) || !Object.op_Implicit((Object) ((Component) playerAvatar).transform.Find("Attacks(Clone)").Find(str1 + "(Clone)").Find(str2 + "(Clone)")))
        return;
      tk2dSpriteAnimator component = ((Component) ((Component) playerAvatar).transform.Find("Attacks(Clone)").Find(str1 + "(Clone)").Find(str2 + "(Clone)")).GetComponent<tk2dSpriteAnimator>();
      Debug.Log((object) ("获取动画：" + str3));
      ((Component) component).gameObject.GetComponent<AttackAnimTimeCounter>().SetRemainDuration(component.Library.GetClipByName(str3).Duration);
      component.Play(str3.TrimStart());
    }

    private void CreateNewPlayer(CSteamID steamID, Vector3 position)
    {
      GameObject gameObject = Object.Instantiate<GameObject>(new GameObject("Player_Clone"), position, Quaternion.identity);
      gameObject.AddComponent<PlayerAvatar>();
      Object.DontDestroyOnLoad((Object) gameObject);
      tk2dSprite tk2dSprite = gameObject.AddComponent<tk2dSprite>();
      tk2dSpriteAnimator tk2dSpriteAnimator = gameObject.AddComponent<tk2dSpriteAnimator>();
      tk2dSpriteAnimator.Library = SilksongMultiplayerAPI.Hero_Hornet.GetComponent<tk2dSpriteAnimator>().Library;
      tk2dSpriteAnimator.Play(SilksongMultiplayerAPI.Hero_Hornet.GetComponent<tk2dSpriteAnimator>().CurrentClip);
      ((tk2dBaseSprite) tk2dSprite).SetSprite(((tk2dBaseSprite) SilksongMultiplayerAPI.Hero_Hornet.GetComponent<tk2dSprite>()).Collection, ((tk2dBaseSprite) SilksongMultiplayerAPI.Hero_Hornet.GetComponent<tk2dSprite>()).spriteId);
      PlayerAvatar component = gameObject.GetComponent<PlayerAvatar>();
      component.Initialize(steamID);
      this.remotePlayers.Add(steamID, component);
    }
  }
}
