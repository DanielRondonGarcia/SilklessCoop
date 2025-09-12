// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.RoomManager
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using GlobalEnums;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
// using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
// using UnityEngine.ResourceManagement.AsyncOperations;
// using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


#nullable enable
namespace SilksongMultiplayer
{
  internal class RoomManager : MonoBehaviour
  {
    public CSteamID currentRoomID;
    public CSteamID playerID;
    public bool host = false;
    public bool enterRoom = false;
    private bool findAgent = false;
    private bool createButton = false;

    public void CreateRoom() => SteamMatchmaking.CreateLobby((ELobbyType) 2, 200);

    public void Start() => this.Init();

    public void Invite() => SteamFriends.ActivateGameOverlayInviteDialog(this.currentRoomID);

    public void Update()
    {
      if (Input.GetKeyDown((KeyCode) 290))
        Object.Instantiate<GameObject>(new GameObject("banana_Clone"), SilksongMultiplayerAPI.Hero_Hornet.transform.position, Quaternion.identity).AddComponent<Dummy>();
      if (Input.GetKeyDown((KeyCode) 292))
        HeroController.instance.TakeDamage(((Component) this).gameObject, (CollisionSide) 4, 1, (HazardType) 1, (DamagePropertyFlags) 0);
      if (!SilksongMultiplayerAPI.enablePvP || !Object.op_Implicit((Object) GameObject.Find("Mapper NPC")) || !Object.op_Implicit((Object) GameObject.Find("Mapper NPC").transform.Find("Enemy Range")))
        return;
      ((Component) GameObject.Find("Mapper NPC").transform.Find("Enemy Range")).gameObject.SetActive(false);
    }

    public void Init()
    {
      // ISSUE: method pointer
      Callback<LobbyEnter_t>.Create(new Callback<LobbyEnter_t>.DispatchDelegate((object) this, __methodptr(OnLobbyEntered)));
      // ISSUE: method pointer
      Callback<GameLobbyJoinRequested_t>.Create(new Callback<GameLobbyJoinRequested_t>.DispatchDelegate((object) this, __methodptr(OnJoinRequested)));
      // ISSUE: method pointer
      Callback<LobbyCreated_t>.Create(new Callback<LobbyCreated_t>.DispatchDelegate((object) this, __methodptr(OnLobbyCreated)));
      this.playerID = SteamUser.GetSteamID();
      SilksongMultiplayerAPI.RoomManager = this;
    }

    public static GameObject FindObjectInScene(Scene scene, string objectName, bool clone = true)
    {
      if (!scene.IsValid() || !scene.isLoaded)
      {
        Debug.LogError((object) "Scene 无效或未加载");
        return (GameObject) null;
      }
      GameObject objectInScene1 = (GameObject) null;
      foreach (GameObject rootGameObject in scene.GetRootGameObjects())
      {
        if (((Object) rootGameObject).name == objectName)
        {
          objectInScene1 = rootGameObject;
          break;
        }
        Transform transform = rootGameObject.transform.Find(objectName);
        if (Object.op_Inequality((Object) transform, (Object) null))
        {
          objectInScene1 = ((Component) transform).gameObject;
          break;
        }
      }
      if (Object.op_Equality((Object) objectInScene1, (Object) null))
      {
        Debug.LogWarning((object) ("在场景 " + scene.name + " 中未找到对象: " + objectName));
        return (GameObject) null;
      }
      if (!clone)
        return objectInScene1;
      GameObject objectInScene2 = Object.Instantiate<GameObject>(objectInScene1);
      ((Object) objectInScene2).name = ((Object) objectInScene1).name + "_Copy";
      return objectInScene2;
    }

    public void OnLobbyCreated(LobbyCreated_t result)
    {
      if (result.m_eResult != 1)
        return;
      this.currentRoomID = new CSteamID(result.m_ulSteamIDLobby);
      SteamMatchmaking.SetLobbyData(this.currentRoomID, "name", SteamFriends.GetPersonaName().ToString());
      Debug.Log((object) ("大厅创建成功，ID: " + this.currentRoomID.ToString()));
      this.enterRoom = true;
    }

    public void OnJoinRequested(GameLobbyJoinRequested_t callback)
    {
      Debug.Log((object) ("收到来自玩家 " + SteamFriends.GetFriendPersonaName(callback.m_steamIDFriend) + " 的邀请"));
      SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
      this.host = false;
    }

    public void OnLobbyEntered(LobbyEnter_t callback)
    {
      this.currentRoomID = new CSteamID(callback.m_ulSteamIDLobby);
      this.enterRoom = true;
      SilksongMultiplayerAPI.enterRoom = true;
      int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(this.currentRoomID);
      for (int index = 0; index < numLobbyMembers; ++index)
      {
        CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(this.currentRoomID, index);
        Debug.Log((object) ("大厅成员 " + SteamFriends.GetFriendPersonaName(lobbyMemberByIndex) + " SteamID: " + lobbyMemberByIndex.ToString()));
      }
    }

    private void FixedUpdate()
    {
      SteamAPI.RunCallbacks();
      if (!SilksongMultiplayerAPI.startGame && Object.op_Implicit((Object) GameObject.Find("Hero_Hornet(Clone)")))
      {
        SilksongMultiplayerAPI.startGame = true;
        SilksongMultiplayerAPI.Hero_Hornet = GameObject.Find("Hero_Hornet(Clone)");
        SilksongMultiplayerAPI.Hero_Hornet.AddComponent<PlayerNetworkSync>();
        SilksongMultiplayerAPI.Hero_Hornet.GetComponent<PlayerNetworkSync>().currentRoomID = this.currentRoomID;
        // Addressables functionality commented out for compilation
        // AsyncOperationHandle<SceneInstance> asyncOperationHandle = Addressables.LoadSceneAsync((object) "Scenes/Tut_02", (LoadSceneMode) 1, true, 100, (SceneReleaseMode) 0);
        // Scene unityScene = new Scene();
        // asyncOperationHandle.Completed += (Action<AsyncOperationHandle<SceneInstance>>) (op =>
        // {
        //   if (op.Status != 1)
        //     return;
        //   SceneInstance result = op.Result;
        //   unityScene = result.Scene;
        // });
        // asyncOperationHandle.Completed += (Action<AsyncOperationHandle<SceneInstance>>) (op =>
        // {
        //   GameObject objectInScene = RoomManager.FindObjectInScene(unityScene, "MossBone Crawler");
        //   Debug.Log((object) "已找到");
        //   SilksongMultiplayerAPI.sampleEnemyHitEffectsProfile = objectInScene.GetComponent<EnemyHitEffectsRegular>().Profile;
        //   SceneManager.UnloadSceneAsync(unityScene);
        // });
        SilksongMultiplayerAPI.Hunter_v3 = ToolItemManager.GetCrestByName("Hunter_v3");
        SilksongMultiplayerAPI.Reaper = ToolItemManager.GetCrestByName("Reaper");
        SilksongMultiplayerAPI.Wanderer = ToolItemManager.GetCrestByName("Wanderer");
        SilksongMultiplayerAPI.Warrior = ToolItemManager.GetCrestByName("Warrior");
        SilksongMultiplayerAPI.Witch = ToolItemManager.GetCrestByName("Witch");
        SilksongMultiplayerAPI.Toolmaster = ToolItemManager.GetCrestByName("Toolmaster");
        SilksongMultiplayerAPI.Spell = ToolItemManager.GetCrestByName("Spell");
      }
      if (!Object.op_Implicit((Object) GameObject.Find("OptionsButton")) || this.createButton)
        return;
      this.createButton = true;
      GameObject gameObject = Object.Instantiate<GameObject>(GameObject.Find("OptionsButton"), GameObject.Find("OptionsButton").transform.parent);
      ((Component) gameObject.transform.GetChild(0)).GetComponent<Text>().text = "创建大厅";
      ((Behaviour) gameObject.GetComponent<EventTrigger>()).enabled = false;
      gameObject.AddComponent<CreateLobbyButton>();
    }

    public List<CSteamID> GetRoomMembers()
    {
      int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(SilksongMultiplayerAPI.RoomManager.currentRoomID);
      List<CSteamID> roomMembers = new List<CSteamID>();
      for (int index = 0; index < numLobbyMembers; ++index)
      {
        CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(SilksongMultiplayerAPI.RoomManager.currentRoomID, index);
        roomMembers.Add(lobbyMemberByIndex);
      }
      return roomMembers;
    }
  }
}
