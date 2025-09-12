// Decompiled with JetBrains decompiler
// Type: SilksongMultiplayer.PlayerAvatar
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using GlobalEnums;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


#nullable enable
namespace SilksongMultiplayer
{
  internal class PlayerAvatar : MonoBehaviour
  {
    private Vector3 targetPosition;
    private Vector3 savedPosition;
    private float movingProgress = 0.0f;
    private float lastUpdateTime;
    private float interpolationDelay = 0.1f;
    private Vector3 compassTargetPosition;
    private Vector3 compassSavedPosition;
    private Vector3 wideCompassTargetPosition;
    private Vector3 wideCompassSavedPosition;
    private float compassMovingProgress = 0.0f;
    private CSteamID steamID;
    public string mapName;
    private float createColliderCounter = 3f;
    private Canvas canva;
    private GameObject nameText;
    public GameObject compassIcon;
    public GameObject wideCompassIcon;
    // private Dummy dummy; // Commented out - Dummy class excluded from compilation

    public void UpdatePosition(Vector3 newPosition, float facing)
    {
      this.targetPosition = newPosition;
      this.savedPosition = ((Component) this).transform.position;
      this.movingProgress = 0.0f;
      ((Component) this).transform.localScale = new Vector3(facing, 1f, 1f);
    }

    public void UpdateCompassPosition(Vector2 compass, Vector2 wideCompass)
    {
      this.compassTargetPosition = Vector2.op_Implicit(compass);
      this.compassSavedPosition = this.compassIcon.transform.localPosition;
      this.wideCompassTargetPosition = Vector2.op_Implicit(wideCompass);
      this.wideCompassSavedPosition = this.wideCompassIcon.transform.localPosition;
      this.compassMovingProgress = 0.0f;
    }

    public void UpdateMap(string mapName_get) => this.mapName = mapName_get;

    public void Initialize(CSteamID steamID)
    {
      this.steamID = steamID;
      Debug.Log((object) string.Format("玩家对象已初始化，SteamID: {0}", (object) steamID));
      GameObject gameObject = new GameObject("nameCanva");
      gameObject.transform.SetPositionAndRotation(((Component) this).transform.position, Quaternion.identity);
      gameObject.transform.SetParent(((Component) this).transform);
      this.canva = gameObject.AddComponent<Canvas>();
      this.canva.renderMode = (RenderMode) 1;
      this.canva.sortingLayerName = "HUD";
      this.canva.sortingLayerID = 629535577;
      this.canva.sortingOrder = 50;
      gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(2560f, 1440f);
      this.nameText = new GameObject("nameText");
      this.nameText.transform.SetParent(gameObject.transform);
      this.nameText.AddComponent<CanvasRenderer>();
      this.nameText.transform.localScale = Vector3.op_Multiply(Vector3.one, 0.01f);
      Text text = this.nameText.AddComponent<Text>();
      text.text = SteamFriends.GetFriendPersonaName(steamID);
      text.font = SilksongMultiplayerAPI.savedFont;
      text.fontSize = 50;
      text.alignment = (TextAnchor) 4;
      ulong num1 = 76561198929282998;
      ulong num2 = 76561199835946204;
      if ((long) steamID.m_SteamID == (long) num1 || (long) steamID.m_SteamID == (long) num2)
        ((Graphic) text).color = Color.yellow;
      this.compassIcon = Object.Instantiate<GameObject>(SilksongMultiplayerAPI.compassIcon, DDOLFinder.FindInDDOLByName("Game_Map_Hornet(Clone)")[0].transform);
      ((Object) this.compassIcon).name = "compassIconClone";
      this.wideCompassIcon = Object.Instantiate<GameObject>(SilksongMultiplayerAPI.wideCompassIcon, DDOLFinder.FindInDDOLByName("Wide Map(Clone)")[0].transform);
      ((Object) this.wideCompassIcon).name = "wideCompassIconClone";
      NetworkDataSender networkDataSender = SilksongMultiplayerAPI.networkDataSender;
      Scene scene = GameObject.Find("SceneBorder(Clone)").scene;
      string mapName = this.mapName = scene.name;
      networkDataSender.SendMapChangeNotification(mapName);
      Transform transform = SilksongMultiplayerAPI.Hero_Hornet.transform.Find("Attacks");
      GameObject parent = Object.Instantiate<GameObject>(new GameObject("Attacks"), ((Component) this).transform.position, Quaternion.identity, ((Component) this).transform);
      this.CreateAttackEffectForAllChild(PlayerAvatar.FindInactiveChildByName(((Component) transform).gameObject, "Default"), parent);
      this.CreateAttackEffectForAllChild(PlayerAvatar.FindInactiveChildByName(((Component) transform).gameObject, "Cloakless"), parent);
      this.CreateAttackEffectForAllChild(PlayerAvatar.FindInactiveChildByName(((Component) transform).gameObject, "Scythe"), parent);
      this.CreateAttackEffectForAllChild(PlayerAvatar.FindInactiveChildByName(((Component) transform).gameObject, "Warrior"), parent);
      this.CreateAttackEffectForAllChild(PlayerAvatar.FindInactiveChildByName(((Component) transform).gameObject, "Wanderer"), parent);
      this.CreateAttackEffectForAllChild(PlayerAvatar.FindInactiveChildByName(((Component) transform).gameObject, "Toolmaster"), parent);
      this.CreateAttackEffectForAllChild(PlayerAvatar.FindInactiveChildByName(((Component) transform).gameObject, "Witch"), parent);
      this.CreateAttackEffectForAllChild(PlayerAvatar.FindInactiveChildByName(((Component) transform).gameObject, "Shaman"), parent);
    }

    public void CreateAttackEffectForAllChild(GameObject collection, GameObject parent)
    {
      if (!(((Object) collection.transform.parent).name == "Attacks"))
        return;
      GameObject gameObject1 = Object.Instantiate<GameObject>(new GameObject(((Object) collection).name), ((Component) this).transform.position, Quaternion.identity, parent.transform);
      Transform[] componentsInChildren = ((Component) collection.transform).GetComponentsInChildren<Transform>(true);
      foreach (Transform transform in componentsInChildren)
      {
        if (((Object) transform.parent.parent).name == "Attacks")
        {
          GameObject gameObject2 = Object.Instantiate<GameObject>(new GameObject(((Object) transform).name), ((Component) this).transform.position, Quaternion.identity, gameObject1.transform);
          gameObject2.transform.localPosition = transform.localPosition;
          gameObject2.AddComponent<AttackAnimTimeCounter>();
          SilksongMultiplayerAPI.CloneAnimatorOfObject(gameObject2, ((Component) transform).gameObject);
        }
      }
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

    private void Update()
    {
      ((Component) this).transform.position = Vector3.Lerp(this.savedPosition, this.targetPosition, this.movingProgress);
      this.movingProgress += Time.deltaTime / 0.03f;
      this.compassIcon.SetActive(SilksongMultiplayerAPI.compassIcon.activeSelf);
      this.compassIcon.transform.localPosition = Vector3.op_Addition(Vector2.op_Implicit(Vector2.Lerp(Vector2.op_Implicit(this.compassSavedPosition), Vector2.op_Implicit(this.compassTargetPosition), this.compassMovingProgress)), new Vector3(0.0f, 0.0f, -5f));
      this.wideCompassIcon.SetActive(SilksongMultiplayerAPI.wideCompassIcon.activeSelf);
      this.wideCompassIcon.transform.localPosition = Vector3.op_Addition(Vector2.op_Implicit(Vector2.Lerp(Vector2.op_Implicit(this.wideCompassSavedPosition), Vector2.op_Implicit(this.wideCompassTargetPosition), this.compassMovingProgress)), new Vector3(0.0f, 0.0f, -2f));
      this.compassMovingProgress += Time.deltaTime / 0.03f;
      ((Component) this.canva).transform.localPosition = Vector3.op_Addition(Vector3.zero, new Vector3(0.0f, 2.5f, 0.0f));
      this.nameText.transform.localPosition = Vector3.zero;
      this.nameText.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 50f);
      if ((double) ((Component) this).transform.localScale.x < 0.0)
        ((Component) this.canva).transform.localScale = Vector2.op_Implicit(new Vector2(-1f, 1f));
      else
        ((Component) this.canva).transform.localScale = Vector2.op_Implicit(new Vector2(1f, 1f));
      if ((double) Vector3.Distance(this.savedPosition, this.targetPosition) > 5.0)
        this.movingProgress = 1f;
      if (Object.op_Implicit((Object) GameObject.Find("SceneBorder(Clone)")))
      {
        Scene scene = GameObject.Find("SceneBorder(Clone)").scene;
        if (scene.name == this.mapName)
          this.Hide(false);
        else
          this.Hide(true);
      }
      if ((double) this.createColliderCounter < 0.0 && (double) this.createColliderCounter > -100.0)
      {
        if (SilksongMultiplayerAPI.enablePvP)
        {
          GameObject gameObject = new GameObject("hitbox");
          gameObject.transform.SetPositionAndRotation(((Component) this).transform.position, Quaternion.identity);
          gameObject.transform.SetParent(((Component) this).transform);
          this.dummy = gameObject.AddComponent<Dummy>();
          this.dummy.hero = true;
        }
        this.canva.renderMode = (RenderMode) 2;
        ((Component) this.canva).transform.localPosition = Vector3.zero;
        this.nameText.transform.localPosition = Vector3.zero;
        this.createColliderCounter = -100f;
      }
      else
        this.createColliderCounter -= Time.deltaTime;
    }

    private void Hide(bool hide)
    {
      if (hide)
      {
        ((tk2dBaseSprite) ((Component) this).GetComponent<tk2dSprite>()).color = new Color(1f, 1f, 1f, 0.0f);
        this.nameText.GetComponent<Text>().text = string.Empty;
      }
      else
      {
        ((tk2dBaseSprite) ((Component) this).GetComponent<tk2dSprite>()).color = new Color(1f, 1f, 1f, 1f);
        this.nameText.GetComponent<Text>().text = SteamFriends.GetFriendPersonaName(this.steamID);
      }
    }

    public void HitByHero(HitInstance hitInstance)
    {
      int direction = (double) hitInstance.Direction >= 45.0 ? ((double) hitInstance.Direction >= 135.0 ? ((double) hitInstance.Direction >= 225.0 ? ((double) hitInstance.Direction >= 315.0 ? 1 : 0) : 2) : 3) : 1;
      float damageDealt = (float) hitInstance.DamageDealt;
      SilksongMultiplayerAPI.networkDataSender.SendTargetHeroTakeDamageData(this.steamID.m_SteamID, Mathf.FloorToInt(damageDealt / 5f), direction, 1, (int) hitInstance.AttackType);
    }

    public void HitEffect(CollisionSide direction, int damage, AttackTypes attackType)
    {
      // ISSUE: unable to decompile the method.
    }
  }
}
