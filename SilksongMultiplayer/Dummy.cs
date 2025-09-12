// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.Dummy
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using System.IO;
using UnityEngine;


#nullable enable
namespace SilksongMultiplayer
{
  internal class Dummy : MonoBehaviour, IHitResponder
  {
    private HealthManager hm;
    public EnemyHitEffectsRegular hitEffectsRegular;
    public bool hero = false;

    public void Start()
    {
      SpriteRenderer spriteRenderer = ((Component) this).gameObject.AddComponent<SpriteRenderer>();
      byte[] numArray = File.ReadAllBytes(Path.Combine(Path.Combine(Path.GetDirectoryName(Application.dataPath), "BepInEx", "plugins", "XvX"), "myImage.png"));
      Texture2D texture2D = new Texture2D(1, 1);
      ImageConversion.LoadImage(texture2D, numArray);
      Sprite sprite = Sprite.Create(texture2D, new Rect(0.0f, 0.0f, 0.5f, 1f), new Vector2(0.5f, 0.5f));
      spriteRenderer.sprite = sprite;
      BoxCollider2D boxCollider2D = ((Component) this).gameObject.AddComponent<BoxCollider2D>();
      ((Component) this).gameObject.layer = 11;
      ((Collider2D) boxCollider2D).isTrigger = true;
      ((Component) this).gameObject.AddComponent<PersonalObjectPool>();
      this.hitEffectsRegular = ((Component) this).gameObject.AddComponent<EnemyHitEffectsRegular>();
      this.hm = ((Component) this).gameObject.AddComponent<HealthManager>();
      this.hm.SetDamageScalingToCustom();
      SilksongMultiplayerAPI.ReplaceItemDropGroups(this.hm);
      this.hitEffectsRegular.Profile = SilksongMultiplayerAPI.sampleEnemyHitEffectsProfile;
      this.hm.hp = 1000;
    }

    public IHitResponder.HitResponse Hit(HitInstance hitInstance)
    {
      Debug.Log((object) ("attack type: " + hitInstance.AttackType.ToString() + " Damage: " + hitInstance.DamageDealt.ToString()));
      this.hitEffectsRegular.ReceiveHitEffect(hitInstance);
      this.hm.hp = 1000;
      if (this.hero)
        ((Component) ((Component) this).transform.parent).GetComponent<PlayerAvatar>().HitByHero(hitInstance);
      return IHitResponder.HitResponse.op_Implicit((IHitResponder.Response) 0);
    }
  }
}
