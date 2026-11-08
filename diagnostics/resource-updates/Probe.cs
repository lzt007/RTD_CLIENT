using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Standalone candidate only. Never linked into the default release.
public sealed class RTDResourceProbe : MonoBehaviour {
 public static void StartProbe() {
  string root=Application.persistentDataPath;
  if(!File.Exists(Path.Combine(root,"rtd-resource-probe")))return;
  GameObject go=new GameObject("RTDResourceProbe");
  UnityEngine.Object.DontDestroyOnLoad(go);
  go.AddComponent<RTDResourceProbe>();
 }
 IEnumerator Start() {
  string root=Application.persistentDataPath;
  File.Delete(Path.Combine(root,"rtd-resource-probe"));
  Debug.Log("RTD_UPDATE_PROBE begin");
  WWW first=new WWW("file://"+Path.Combine(root,"rtd-probe-a.unity3d"));
  yield return first;
  if(!String.IsNullOrEmpty(first.error)){Debug.LogError("RTD_UPDATE_PROBE first error="+first.error);yield break;}
  AssetBundle old=first.assetBundle;
  if(old==null){Debug.LogError("RTD_UPDATE_PROBE first null");yield break;}
  UnityEngine.Object[] oldObjects=new UnityEngine.Object[]{old.Load("ms00_book_el_05",typeof(GameObject))};
  Debug.Log("RTD_UPDATE_PROBE old objects="+oldObjects.Length);
  WWW second=new WWW("file://"+Path.Combine(root,"rtd-probe-b.unity3d"));yield return second;
  AssetBundle fresh=second.assetBundle;
  Debug.Log("RTD_UPDATE_PROBE coexist="+(fresh!=null)+" error="+second.error);
  if(fresh!=null)fresh.Unload(true);
  old.Unload(false);first.Dispose();second.Dispose();
  Debug.Log("RTD_UPDATE_PROBE retained="+(oldObjects.Length>0&&oldObjects[0]!=null));
  WWW third=new WWW("file://"+Path.Combine(root,"rtd-probe-b.unity3d"));yield return third;
  AssetBundle replacement=third.assetBundle;
  if(replacement==null){Debug.LogError("RTD_UPDATE_PROBE reload null error="+third.error);yield break;}
  UnityEngine.Object[] objects=new UnityEngine.Object[]{replacement.Load("ms00_book_el_05",typeof(GameObject))};
  Debug.Log("RTD_UPDATE_PROBE reloaded="+objects.Length+" oldRetained="+(oldObjects.Length>0&&oldObjects[0]!=null));
  replacement.Unload(true);third.Dispose();
  WWW rollback=new WWW("file://"+Path.Combine(root,"rtd-probe-a.unity3d"));yield return rollback;
  AssetBundle restored=rollback.assetBundle;
  Debug.Log("RTD_UPDATE_PROBE rollback="+(restored!=null));
  if(restored!=null)restored.Unload(true);
  rollback.Dispose();
  foreach(UnityEngine.Object item in oldObjects)if(item!=null)UnityEngine.Object.Destroy(item);
  Debug.Log("RTD_UPDATE_PROBE complete");
 }
}
