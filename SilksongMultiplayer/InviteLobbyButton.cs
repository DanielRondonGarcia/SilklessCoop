// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.InviteLobbyButton
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


#nullable enable
namespace SilksongMultiplayer
{
  public class InviteLobbyButton : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
  {
    public void OnPointerClick(PointerEventData pointerEventData)
    {
      if (!SilksongMultiplayerAPI.RoomManager.enterRoom)
        return;
      SilksongMultiplayerAPI.RoomManager.Invite();
    }

    public void Update()
    {
      ((Component) ((Component) this).transform.GetChild(0)).GetComponent<Text>().text = "邀请好友";
      if (!Object.op_Implicit((Object) ((Component) this).GetComponent<EventTrigger>()))
        return;
      ((Behaviour) ((Component) this).GetComponent<EventTrigger>()).enabled = false;
    }
  }
}
