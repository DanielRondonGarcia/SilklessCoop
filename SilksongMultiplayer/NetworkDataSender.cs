// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.NetworkDataSender
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using Steamworks;
using System;
using System.Text;
using UnityEngine;


#nullable enable
namespace SilksongMultiplayer
{
  internal class NetworkDataSender
  {
    public void SendAnimationData(string animationName, int extraValue)
    {
      byte[] src = new byte[1]{ (byte) 2 };
      byte[] bytes1 = Encoding.UTF8.GetBytes(animationName);
      byte length = (byte) bytes1.Length;
      byte[] bytes2 = BitConverter.GetBytes(extraValue);
      byte[] dst = new byte[2 + bytes1.Length + bytes2.Length];
      int dstOffset1 = 0;
      Buffer.BlockCopy((Array) src, 0, (Array) dst, dstOffset1, 1);
      int num1 = dstOffset1 + 1;
      byte[] numArray = dst;
      int index = num1;
      int dstOffset2 = index + 1;
      int num2 = (int) length;
      numArray[index] = (byte) num2;
      Buffer.BlockCopy((Array) bytes1, 0, (Array) dst, dstOffset2, bytes1.Length);
      int dstOffset3 = dstOffset2 + bytes1.Length;
      Buffer.BlockCopy((Array) bytes2, 0, (Array) dst, dstOffset3, bytes2.Length);
      int num3 = dstOffset3 + bytes2.Length;
      foreach (CSteamID roomMember in SilksongMultiplayerAPI.GetRoomMembers())
      {
        if (CSteamID.op_Inequality(roomMember, SteamUser.GetSteamID()))
          SteamNetworking.SendP2PPacket(roomMember, dst, (uint) dst.Length, (EP2PSend) 0, 0);
      }
    }

    public void SendMapChangeNotification(string mapName)
    {
      byte[] src = new byte[1]{ (byte) 3 };
      byte[] bytes = Encoding.UTF8.GetBytes(mapName);
      byte length = (byte) bytes.Length;
      byte[] dst = new byte[2 + bytes.Length];
      Buffer.BlockCopy((Array) src, 0, (Array) dst, 0, 1);
      dst[1] = length;
      Buffer.BlockCopy((Array) bytes, 0, (Array) dst, 2, bytes.Length);
      foreach (CSteamID roomMember in SilksongMultiplayerAPI.GetRoomMembers())
      {
        if (CSteamID.op_Inequality(roomMember, SteamUser.GetSteamID()))
          SteamNetworking.SendP2PPacket(roomMember, dst, (uint) dst.Length, (EP2PSend) 2, 0);
      }
    }

    public void SendTargetHeroTakeDamageData(
      ulong targetSteamId,
      int damage,
      int direction,
      int hazardType,
      int attackTypes)
    {
      byte[] src = new byte[1]{ (byte) 4 };
      byte[] bytes1 = BitConverter.GetBytes(targetSteamId);
      byte[] bytes2 = BitConverter.GetBytes(damage);
      byte[] bytes3 = BitConverter.GetBytes(direction);
      byte[] bytes4 = BitConverter.GetBytes(hazardType);
      byte[] bytes5 = BitConverter.GetBytes(attackTypes);
      byte[] dst = new byte[1 + bytes1.Length + bytes2.Length + bytes3.Length + bytes4.Length + bytes5.Length];
      int dstOffset1 = 0;
      Buffer.BlockCopy((Array) src, 0, (Array) dst, dstOffset1, 1);
      int dstOffset2 = dstOffset1 + 1;
      Buffer.BlockCopy((Array) bytes1, 0, (Array) dst, dstOffset2, bytes1.Length);
      int dstOffset3 = dstOffset2 + bytes1.Length;
      Buffer.BlockCopy((Array) bytes2, 0, (Array) dst, dstOffset3, bytes2.Length);
      int dstOffset4 = dstOffset3 + bytes2.Length;
      Buffer.BlockCopy((Array) bytes3, 0, (Array) dst, dstOffset4, bytes3.Length);
      int dstOffset5 = dstOffset4 + bytes3.Length;
      Buffer.BlockCopy((Array) bytes4, 0, (Array) dst, dstOffset5, bytes4.Length);
      int dstOffset6 = dstOffset5 + bytes4.Length;
      Buffer.BlockCopy((Array) bytes5, 0, (Array) dst, dstOffset6, bytes5.Length);
      int num = dstOffset6 + bytes5.Length;
      foreach (CSteamID roomMember in SilksongMultiplayerAPI.GetRoomMembers())
      {
        if (CSteamID.op_Inequality(roomMember, SteamUser.GetSteamID()))
          SteamNetworking.SendP2PPacket(roomMember, dst, (uint) dst.Length, (EP2PSend) 2, 0);
      }
    }

    public void SendHeroAttackAnimationData(string parentName, string name, string animationName)
    {
      byte[] src = new byte[1]{ (byte) 6 };
      byte[] bytes1 = Encoding.UTF8.GetBytes(parentName);
      byte[] bytes2 = Encoding.UTF8.GetBytes(name);
      byte[] bytes3 = Encoding.UTF8.GetBytes(animationName);
      if (bytes1.Length > (int) byte.MaxValue || bytes2.Length > (int) byte.MaxValue || bytes3.Length > (int) byte.MaxValue)
      {
        Debug.LogError((object) "[Net] 字符串太长，无法发送！");
      }
      else
      {
        byte[] dst = new byte[1 + (1 + bytes1.Length) + (1 + bytes2.Length) + (1 + bytes3.Length)];
        int dstOffset1 = 0;
        Buffer.BlockCopy((Array) src, 0, (Array) dst, dstOffset1, 1);
        int num1 = dstOffset1 + 1;
        byte[] numArray1 = dst;
        int index1 = num1;
        int dstOffset2 = index1 + 1;
        int length1 = (int) (byte) bytes1.Length;
        numArray1[index1] = (byte) length1;
        Buffer.BlockCopy((Array) bytes1, 0, (Array) dst, dstOffset2, bytes1.Length);
        int num2 = dstOffset2 + bytes1.Length;
        byte[] numArray2 = dst;
        int index2 = num2;
        int dstOffset3 = index2 + 1;
        int length2 = (int) (byte) bytes2.Length;
        numArray2[index2] = (byte) length2;
        Buffer.BlockCopy((Array) bytes2, 0, (Array) dst, dstOffset3, bytes2.Length);
        int num3 = dstOffset3 + bytes2.Length;
        byte[] numArray3 = dst;
        int index3 = num3;
        int dstOffset4 = index3 + 1;
        int length3 = (int) (byte) bytes3.Length;
        numArray3[index3] = (byte) length3;
        Buffer.BlockCopy((Array) bytes3, 0, (Array) dst, dstOffset4, bytes3.Length);
        int num4 = dstOffset4 + bytes3.Length;
        foreach (CSteamID roomMember in SilksongMultiplayerAPI.GetRoomMembers())
        {
          if (CSteamID.op_Inequality(roomMember, SteamUser.GetSteamID()))
            SteamNetworking.SendP2PPacket(roomMember, dst, (uint) dst.Length, (EP2PSend) 0, 0);
        }
      }
    }
  }
}
