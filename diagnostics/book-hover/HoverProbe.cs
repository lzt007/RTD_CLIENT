using System;
using System.Collections;
using System.IO;
using UnityEngine;
public sealed class RTDHoverProbe : MonoBehaviour {
 public static void StartProbe(){GameObject g=new GameObject("RTDHoverProbe");UnityEngine.Object.DontDestroyOnLoad(g);g.AddComponent<RTDHoverProbe>();}
 static string State(Animation a){string s="enabled="+a.enabled+" playing="+a.isPlaying;foreach(AnimationState v in a)s+=" ["+v.name+" en="+v.enabled+" t="+v.time+" w="+v.weight+"]";return s;}
 IEnumerator Sample(GameObject g,string phase){Transform m=g.transform.Find("move");Transform bone=(g.transform.Find("move/book_hover")??g.transform.Find("book_hover")??g.transform.Find("move"));float min=999,max=-999;for(int i=0;i<20;i++){yield return new WaitForSeconds(0.1f);float y=bone.localPosition.y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);if(i==0||i==19)Debug.Log("RTD_HOVER_PROBE "+phase+" boneY="+y+" moveY="+m.localPosition.y+" root="+State(g.animation)+" child="+State(m.animation));}Debug.Log("RTD_HOVER_PROBE "+phase+" min="+min+" max="+max+" delta="+(max-min));}
 float lastScan;
 void Update(){
  if(Time.realtimeSinceStartup-lastScan<1)return;
  lastScan=Time.realtimeSinceStartup;
  foreach(SkinnedMeshRenderer r in UnityEngine.Object.FindObjectsOfType(typeof(SkinnedMeshRenderer))){
   Transform m=r.transform.Find("move"),hover=r.transform.Find("move/book_hover");
   if(r.transform.position.x>9000||m==null||(!r.gameObject.name.StartsWith("ast_ms00_book_el_03")&&hover==null))continue;
   Transform bone=hover??r.transform.Find("book_hover")??m;
   Debug.Log("RTD_HOVER_LIVE bone="+bone.name+" boneY="+bone.localPosition.y+" moveY="+m.localPosition.y+" root="+State(r.animation)+" child="+State(m.animation));
  }
 }
 IEnumerator Start(){
  yield return new WaitForSeconds(3);
  string path=Path.Combine(Application.persistentDataPath,"hover-book.unity3d");
  if(!File.Exists(path)){Debug.Log("RTD_HOVER_PROBE missing file");yield break;}
  WWW w=new WWW("file://"+path);yield return w;
  AssetBundle b=w.assetBundle;if(b==null){Debug.Log("RTD_HOVER_PROBE bundle null");yield break;}
  GameObject asset=b.Load("ast_ms00_book_el_03",typeof(GameObject)) as GameObject;
  GameObject g=UnityEngine.Object.Instantiate(asset) as GameObject;g.transform.position=new Vector3(10000,10000,10000);g.SetActiveRecursively(true);
  Debug.Log("RTD_HOVER_PROBE begin");yield return StartCoroutine(Sample(g,"autoplay"));
  Transform m=g.transform.Find("move");GameObject motion=Resources.Load("AnimationData/Motion/pc_wait_stk_01") as GameObject;
  Debug.Log("RTD_HOVER_PROBE motion="+(motion!=null));
  if(motion!=null){m.animation.AddClip(motion.animation.clip,"Wait");m.animation.Stop();m.animation.CrossFade("Wait");yield return StartCoroutine(Sample(g,"with_wait"));m.animation.Stop();yield return StartCoroutine(Sample(g,"child_stopped"));}
  UnityEngine.Object.Destroy(g);b.Unload(false);w.Dispose();Debug.Log("RTD_HOVER_PROBE complete");
 }
}
