// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.CreateLobbyButton
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


#nullable enable
namespace SilksongMultiplayer
{
  internal class CreateLobbyButton : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
  {
    public void OnPointerClick(PointerEventData pointerEventData)
    {
      SilksongMultiplayerAPI.RoomManager.CreateRoom();
      GameObject gameObject = Object.Instantiate<GameObject>(GameObject.Find("OptionsButton"), GameObject.Find("OptionsButton").transform.parent);
      ((Component) gameObject.transform.GetChild(0)).GetComponent<Text>().text = "邀请好友";
      gameObject.AddComponent<InviteLobbyButton>();
      Object.Destroy((Object) ((Component) this).gameObject);
    }

    public void Update()
    {
      ((Component) ((Component) this).transform.GetChild(0)).GetComponent<Text>().text = "创建大厅";
      SilksongMultiplayerAPI.savedFont = ((Component) GameObject.Find("OptionsButton").transform.GetChild(0)).GetComponent<Text>().font;
      if (!Object.op_Implicit((Object) ((Component) this).GetComponent<EventTrigger>()))
        return;
      ((Behaviour) ((Component) this).GetComponent<EventTrigger>()).enabled = false;
    }
  }
}
