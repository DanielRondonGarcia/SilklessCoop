// Decompiled with JetBrains decompiler
// Type: DDOLFinder
// Assembly: SilksongMultiplayer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: BECDDE68-6048-48ED-A34B-DDF6005F3CE9
// Assembly location: D:\SteamLibrary\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\XvX\SilksongMultiplayer.dll

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;


#nullable enable
public static class DDOLFinder
{
  public static Scene GetDDOLScene()
  {
    GameObject gameObject = new GameObject("__ddol_probe__");
    Object.DontDestroyOnLoad((Object) gameObject);
    Scene scene = gameObject.scene;
    Object.DestroyImmediate((Object) gameObject);
    return scene;
  }

  public static GameObject[] GetDDOLRoots()
  {
    Scene ddolScene = DDOLFinder.GetDDOLScene();
    return !ddolScene.IsValid() || !ddolScene.isLoaded ? new GameObject[0] : ddolScene.GetRootGameObjects();
  }

  public static List<GameObject> FindInDDOLByName(string name, bool exact = true)
  {
    List<GameObject> inDdolByName = new List<GameObject>();
    foreach (GameObject ddolRoot in DDOLFinder.GetDDOLRoots())
    {
      foreach (Transform componentsInChild in ddolRoot.GetComponentsInChildren<Transform>(true))
      {
        if (componentsInChild != null && DDOLFinder.IsMatch(componentsInChild.name, name, exact))
          inDdolByName.Add(((Component) componentsInChild).gameObject);
      }
    }
    return inDdolByName;
  }

  public static GameObject FindChildByName(GameObject parent, string childName, bool exact = true)
  {
    if (!Object.op_Implicit((Object) parent))
      return (GameObject) null;
    foreach (Transform componentsInChild in parent.GetComponentsInChildren<Transform>(true))
    {
      if (Object.op_Implicit((Object) componentsInChild) && !Object.op_Equality((Object) ((Component) componentsInChild).gameObject, (Object) parent) && DDOLFinder.IsMatch(((Object) componentsInChild).name, childName, exact))
        return ((Component) componentsInChild).gameObject;
    }
    return (GameObject) null;
  }

  public static GameObject FindChildByPath(GameObject parent, string path)
  {
    if (!Object.op_Implicit((Object) parent) || string.IsNullOrEmpty(path))
      return (GameObject) null;
    Transform transform1 = parent.transform;
    foreach (string str in path.Split('/'))
    {
      Transform transform2 = (Transform) null;
      foreach (Transform componentsInChild in ((Component) transform1).GetComponentsInChildren<Transform>(true))
      {
        if (Object.op_Equality((Object) componentsInChild.parent, (Object) transform1) && ((Object) componentsInChild).name == str)
        {
          transform2 = componentsInChild;
          break;
        }
      }
      if (Object.op_Equality((Object) transform2, (Object) null))
        return (GameObject) null;
      transform1 = transform2;
    }
    return ((Component) transform1).gameObject;
  }

  public static void DumpDDOLTree()
  {
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("=== DDOL Tree ===");
    foreach (GameObject ddolRoot in DDOLFinder.GetDDOLRoots())
      DDOLFinder.DumpNode(ddolRoot.transform, 0, sb);
    Debug.Log((object) sb.ToString());
  }

  private static void DumpNode(Transform tr, int depth, StringBuilder sb)
  {
    sb.Append(' ', depth * 2).Append("- ").Append(((Object) tr).name).Append(" (activeSelf=").Append(((Component) tr).gameObject.activeSelf).Append(")").AppendLine();
    foreach (Transform tr1 in tr)
      DDOLFinder.DumpNode(tr1, depth + 1, sb);
  }

  private static bool IsMatch(string actual, string want, bool exact) => exact ? actual == want : actual == want || actual.StartsWith(want) || actual.Contains(want);
}
