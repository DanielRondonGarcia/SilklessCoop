// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.PlayerNetworkSync
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


#nullable enable
namespace SilksongMultiplayer
{
  internal class PlayerNetworkSync : MonoBehaviour
  {
    public CSteamID currentRoomID;
    private float lastSendTime;
    private const float sendInterval = 0.03f;
    private string mapName;
    private float createColliderCounter = 0.3f;
    private Canvas canva;
    private GameObject nameText;
    private float compassLastSendTime;
    private const float compassSendInterval = 1f;

    private void Start()
    {
      SilksongMultiplayerAPI.playerNetworkSync = this;
      GameObject gameObject = new GameObject("nameCanva");
      gameObject.transform.SetPositionAndRotation(((Component) this).transform.position, Quaternion.identity);
      gameObject.transform.SetParent(((Component) this).transform);
      if (!SilksongMultiplayerAPI.enterRoom)
        return;
      this.canva = gameObject.AddComponent<Canvas>();
      this.canva.renderMode = (RenderMode) 2;
      this.canva.sortingLayerName = "HUD";
      this.canva.sortingLayerID = 629535577;
      this.canva.sortingOrder = 50;
      this.canva.renderMode = (RenderMode) 1;
      gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(2560f, 1440f);
      this.nameText = new GameObject("nameText");
      this.nameText.transform.SetParent(gameObject.transform);
      this.nameText.AddComponent<CanvasRenderer>();
      this.nameText.transform.localScale = Vector3.op_Multiply(Vector3.one, 0.01f);
      Text text = this.nameText.AddComponent<Text>();
      text.text = SteamFriends.GetPersonaName();
      text.font = SilksongMultiplayerAPI.savedFont;
      text.fontSize = 50;
      text.alignment = (TextAnchor) 4;
      ulong num1 = 76561198929282998;
      ulong num2 = 76561199835946204;
      if ((long) SteamUser.GetSteamID().m_SteamID == (long) num1 || (long) SteamUser.GetSteamID().m_SteamID == (long) num2)
        ((Graphic) text).color = Color.yellow;
    }

    private void Update()
    {
      if (Object.op_Equality((Object) SilksongMultiplayerAPI.compassIcon, (Object) null) && DDOLFinder.FindInDDOLByName("Game_Map_Hornet(Clone)").Count > 0)
        SilksongMultiplayerAPI.compassIcon = DDOLFinder.FindChildByName(DDOLFinder.FindInDDOLByName("Game_Map_Hornet(Clone)")[0], "Compass Icon");
      if (Object.op_Equality((Object) SilksongMultiplayerAPI.wideCompassIcon, (Object) null) && DDOLFinder.FindInDDOLByName("Wide Map(Clone)").Count > 0)
        SilksongMultiplayerAPI.wideCompassIcon = DDOLFinder.FindChildByName(DDOLFinder.FindInDDOLByName("Wide Map(Clone)")[0], "Compass Icon");
      if ((double) Time.time - (double) this.lastSendTime >= 0.029999999329447746)
      {
        this.SendPositionToAll();
        this.lastSendTime = Time.time;
      }
      if ((double) Time.time - (double) this.compassLastSendTime >= 1.0)
      {
        this.SendMapPositionToAll();
        this.compassLastSendTime = Time.time;
      }
      Scene scene;
      int num;
      if (Object.op_Implicit((Object) GameObject.Find("SceneBorder(Clone)")))
      {
        scene = GameObject.Find("SceneBorder(Clone)").scene;
        num = scene.name != this.mapName ? 1 : 0;
      }
      else
        num = 0;
      if (num != 0)
      {
        scene = GameObject.Find("SceneBorder(Clone)").scene;
        this.mapName = scene.name;
        SilksongMultiplayerAPI.networkDataSender.SendMapChangeNotification(this.mapName);
      }
      if (SilksongMultiplayerAPI.enterRoom)
      {
        ((Component) this.canva).transform.localPosition = Vector3.op_Addition(Vector3.zero, new Vector3(0.0f, 2.5f, 0.0f));
        this.nameText.transform.localPosition = Vector3.zero;
        this.nameText.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 50f);
        if ((double) ((Component) this).transform.localScale.x < 0.0)
          ((Component) this.canva).transform.localScale = Vector2.op_Implicit(new Vector2(-1f, 1f));
        else
          ((Component) this.canva).transform.localScale = Vector2.op_Implicit(new Vector2(1f, 1f));
      }
      if ((double) this.createColliderCounter < 0.0 && (double) this.createColliderCounter > -100.0)
      {
        if (SilksongMultiplayerAPI.enterRoom)
        {
          this.canva.renderMode = (RenderMode) 2;
          ((Component) this.canva).transform.localPosition = Vector3.zero;
          this.nameText.transform.localPosition = Vector3.zero;
        }
        this.createColliderCounter = -100f;
      }
      else
        this.createColliderCounter -= Time.deltaTime;
    }

    public static GameObject FindInactiveChildByName(GameObject parent, string childName)
    {
      foreach (Transform componentsInChild in parent.GetComponentsInChildren<Transform>(true))
      {
        if (((Object) componentsInChild).name == childName)
          return ((Component) componentsInChild).gameObject;
      }
      return (GameObject) null;
    }

    public GameObject[] FindInActiveObjectsByName(string name)
    {
      List<GameObject> gameObjectList = new List<GameObject>();
      foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
      {
        int num;
        if (Object.op_Implicit((Object) gameObject))
        {
          Scene scene = gameObject.scene;
          num = !scene.IsValid() ? 1 : 0;
        }
        else
          num = 1;
        if (num == 0 && ((Object) gameObject).name == name)
          gameObjectList.Add(gameObject);
      }
      return gameObjectList.ToArray();
    }

    public void SendMapPositionToAll()
    {
      if (Object.op_Equality((Object) SilksongMultiplayerAPI.compassIcon, (Object) null) || Object.op_Equality((Object) SilksongMultiplayerAPI.wideCompassIcon, (Object) null))
        return;
      Vector2 vector2_1 = Vector2.op_Implicit(SilksongMultiplayerAPI.compassIcon.transform.localPosition);
      Vector2 vector2_2 = Vector2.op_Implicit(SilksongMultiplayerAPI.wideCompassIcon.transform.localPosition);
      byte[] src = new byte[1]{ (byte) 5 };
      byte[] bytes1 = BitConverter.GetBytes(vector2_1.x);
      byte[] bytes2 = BitConverter.GetBytes(vector2_1.y);
      byte[] bytes3 = BitConverter.GetBytes(vector2_2.x);
      byte[] bytes4 = BitConverter.GetBytes(vector2_2.y);
      byte[] dst = new byte[17];
      int dstOffset1 = 0;
      Buffer.BlockCopy((Array) src, 0, (Array) dst, dstOffset1, 1);
      int dstOffset2 = dstOffset1 + 1;
      Buffer.BlockCopy((Array) bytes1, 0, (Array) dst, dstOffset2, 4);
      int dstOffset3 = dstOffset2 + 4;
      Buffer.BlockCopy((Array) bytes2, 0, (Array) dst, dstOffset3, 4);
      int dstOffset4 = dstOffset3 + 4;
      Buffer.BlockCopy((Array) bytes3, 0, (Array) dst, dstOffset4, 4);
      int dstOffset5 = dstOffset4 + 4;
      Buffer.BlockCopy((Array) bytes4, 0, (Array) dst, dstOffset5, 4);
      foreach (CSteamID roomMember in SilksongMultiplayerAPI.GetRoomMembers())
      {
        if (CSteamID.op_Inequality(roomMember, SteamUser.GetSteamID()))
          SteamNetworking.SendP2PPacket(roomMember, dst, (uint) dst.Length, (EP2PSend) 0, 0);
      }
    }

    private void SendPositionToAll()
    {
      Vector3 position = ((Component) this).transform.position;
      byte[] src = new byte[1]{ (byte) 1 };
      byte[] bytes1 = BitConverter.GetBytes(position.x);
      byte[] bytes2 = BitConverter.GetBytes(position.y);
      byte[] bytes3 = BitConverter.GetBytes(position.z);
      byte[] bytes4 = BitConverter.GetBytes(((Component) this).transform.localScale.x);
      byte[] dst = new byte[21];
      Buffer.BlockCopy((Array) src, 0, (Array) dst, 0, 1);
      Buffer.BlockCopy((Array) bytes1, 0, (Array) dst, 1, 4);
      Buffer.BlockCopy((Array) bytes2, 0, (Array) dst, 5, 4);
      Buffer.BlockCopy((Array) bytes3, 0, (Array) dst, 9, 4);
      Buffer.BlockCopy((Array) bytes4, 0, (Array) dst, 13, 4);
      foreach (CSteamID roomMember in SilksongMultiplayerAPI.GetRoomMembers())
      {
        if (CSteamID.op_Inequality(roomMember, SteamUser.GetSteamID()))
          SteamNetworking.SendP2PPacket(roomMember, dst, (uint) dst.Length, (EP2PSend) 0, 0);
      }
    }
  }
}
