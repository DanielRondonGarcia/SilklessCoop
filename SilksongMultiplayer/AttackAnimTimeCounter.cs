// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.AttackAnimTimeCounter
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using UnityEngine;

namespace SilksongMultiplayer
{
  internal class AttackAnimTimeCounter : MonoBehaviour
  {
    public float remainDuration = 0.0f;

    private void Update()
    {
      if ((double) this.remainDuration > 0.0)
      {
        this.remainDuration -= Time.deltaTime;
      }
      else
      {
        if ((double) this.remainDuration <= -100.0)
          return;
        if (Object.op_Implicit((Object) ((Component) this).GetComponent<tk2dSprite>()))
          ((tk2dBaseSprite) ((Component) this).GetComponent<tk2dSprite>()).color = new Color(1f, 1f, 1f, 0.0f);
        this.remainDuration = -100f;
      }
    }

    public void SetRemainDuration(float duration)
    {
      this.remainDuration = duration;
      if (!Object.op_Implicit((Object) ((Component) this).GetComponent<tk2dSprite>()))
        return;
      ((tk2dBaseSprite) ((Component) this).GetComponent<tk2dSprite>()).color = new Color(1f, 1f, 1f, 1f);
    }
  }
}
