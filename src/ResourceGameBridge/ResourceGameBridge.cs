using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RTD.ResourceUpdates;

public static class RTDResourceGameBridge {
 static Dictionary<AudioSource,AudioClip> sourceClips=new Dictionary<AudioSource,AudioClip>();
 public static void SetAudioClip(AudioSource source,AudioClip clip){source.clip=clip;sourceClips[source]=clip;}
 static string AudioName(string method,int index){object[] args=new object[]{"",index};typeof(AudioClipLoadHelper).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);return (string)args[0];}
 static Dictionary<string,List<string>> requiredPrefabs=new Dictionary<string,List<string>>();
 static void RequirePrefab(string id,string name){List<string> names;if(!requiredPrefabs.TryGetValue(id,out names)){names=new List<string>();requiredPrefabs.Add(id,names);}if(!names.Contains(name))names.Add(name);}
 static bool lease;static bool startup;static string[] leaseRoots;
 static ResourceUpdateRunner runner;
 static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object owner,string name){return owner.GetType().GetField(name,Fields).GetValue(owner);}
 static void Set(object owner,string name,object value){owner.GetType().GetField(name,Fields).SetValue(owner,value);}
 static ResourceUpdateRunner Runner(){
  if(runner==null){GameObject go=new GameObject("RTDResourceUpdates");UnityEngine.Object.DontDestroyOnLoad(go);runner=go.AddComponent<ResourceUpdateRunner>();}
  if(ResourceRuntime.Game==null)ResourceRuntime.Game=new GameHooks();return runner;
 }
 static string Id(AssetBundleDefine.AssetBundleInfo info){return info.fileName+".unity3d";}
 static void Add(List<string> list,string id){if(!list.Contains(id))list.Add(id);}
 static string[] BasicRoots(){List<string> result=new List<string>();foreach(AssetBundleDefine.AssetBundleInfo info in AssetBundleDefine.m_BasicBundles)Add(result,Id(info));return result.ToArray();}
 static void Basic(List<string> result,int index){if(index<0||index>=AssetBundleDefine.m_BasicBundles.Length)throw new IOException("Invalid basic resource index");Add(result,Id(AssetBundleDefine.m_BasicBundles[index]));}
 public static string[] CollectLevelRoots(){
  requiredPrefabs.Clear();List<string> result=new List<string>();AssetBundleLoader loader=Singleton<AssetBundleLoader>.Instance;Basic(result,0);
  if(ClientData.m_UnitParty!=null)foreach(ClientData.UnitParty member in ClientData.m_UnitParty)if(member!=null){MasterDataRTD.UnitData unit=member.unit;int index=loader.GetAssetBundleID(ref unit);Basic(result,index);RequirePrefab(Id(AssetBundleDefine.m_BasicBundles[index]),unit.master.model);}
  if(ClientData.m_EnemyArrange!=null)foreach(int id in ClientData.m_EnemyArrange.GetUniqueEnemyList()){
   EnemyData enemy=ClientData.GetEnemyData(id);if(enemy==null)throw new IOException("Missing enemy resource data");
   if(enemy.IsUnitEnemy()){
    string book="ms00_book_el_0"+ConvertServerToClient.UnitEnemySymbolToBookColor(enemy.Enemy_Symbol);
    string bookId=Id(AssetBundleDefine.GetEnemyBundleInfo(book));Add(result,bookId);RequirePrefab(bookId,"ast_"+book);
    if(enemy.Zukan_ID>0){int index=loader.GetAssetBundleID(enemy.Zukan_ID);Basic(result,index);RequirePrefab(Id(AssetBundleDefine.m_BasicBundles[index]),enemy.PrefabName);}
    else if(!AssetBundleLoader.IsLocalAsset(enemy.PrefabName)){string enemyId=Id(AssetBundleDefine.GetEnemyBundleInfo(enemy.PrefabName));Add(result,enemyId);RequirePrefab(enemyId,"ast_"+enemy.PrefabName);}
   }else if(!AssetBundleLoader.IsLocalAsset(enemy.PrefabName)){string enemyId=Id(AssetBundleDefine.GetEnemyBundleInfo(enemy.PrefabName));Add(result,enemyId);RequirePrefab(enemyId,"ast_"+enemy.PrefabName);}
  }
  return result.ToArray();
 }
 public static bool StartupChecked(bool original){return original&&!startup;}
 public static void StartStartup(AssetBundleLoader owner){startup=true;Runner().StartCoroutine(Startup(owner));}
 static IEnumerator Startup(AssetBundleLoader owner){
  while(lease)yield return null;lease=true;ResourceOperation operation=new ResourceOperation();
  try {
   leaseRoots=BasicRoots();yield return Runner().StartCoroutine(ResourceRuntime.EnsureCurrent(leaseRoots,operation));
   if(!operation.Ready){ResourceRuntime.Log("startup_unavailable reason="+operation.Error);Set(owner,"m_Phase",AssetBundleLoader.E_LOAD_STEP.ERROR);yield break;}
   leaseRoots=operation.CheckedResources;owner.GetType().GetMethod("RTDDownloadStartOriginal",Fields).Invoke(owner,null);
   while(!owner.m_bLoadFinish)yield return null;
  }finally{startup=false;lease=false;leaseRoots=null;}
 }
 public static IEnumerator WrapLevel(IEnumerator original,MonoBehaviour owner){
  while(lease)yield return null;lease=true;ResourceOperation operation=new ResourceOperation();
  try {
   leaseRoots=CollectLevelRoots();
   yield return Runner().StartCoroutine(ResourceRuntime.EnsureCurrent(leaseRoots,operation));
   if(!operation.Ready)throw new IOException("Checked level resources unavailable");
   leaseRoots=operation.CheckedResources;ResourceRuntime.Log("level_ready resources="+leaseRoots.Length+" snapshot="+operation.Snapshot);
   while(original.MoveNext())yield return original.Current;
  }finally{if(operation!=null&&!operation.Ready)operation.Cancelled=true;IDisposable disposable=original as IDisposable;if(disposable!=null)disposable.Dispose();leaseRoots=null;lease=false;}
 }
 public static IEnumerator WrapSingle(IEnumerator original,AssetBundleLoader owner,int index){
  string id=Id(AssetBundleDefine.m_BasicBundles[index]);bool inside=false;
  if(lease&&leaseRoots!=null)foreach(string root in leaseRoots)if(root==id){inside=true;break;}
  bool own=false;ResourceOperation operation=null;
  try {
   if(!inside){while(lease)yield return null;lease=true;own=true;leaseRoots=new string[]{id};operation=new ResourceOperation();
    yield return Runner().StartCoroutine(ResourceRuntime.EnsureCurrent(leaseRoots,operation));if(!operation.Ready)throw new IOException("Checked resource unavailable");}
   if(owner.AssetBundleCheck(index)){Set(owner,"m_SinglePhase",AssetBundleLoader.E_LOAD_STEP.FINISH);yield break;}
   while(original.MoveNext())yield return original.Current;
  }finally{if(operation!=null&&!operation.Ready)operation.Cancelled=true;IDisposable disposable=original as IDisposable;if(disposable!=null)disposable.Dispose();if(own){leaseRoots=null;lease=false;}}
 }
 sealed class GameHooks:IResourceGameHooks {
  Dictionary<int,AssetBundle> savedBasic;List<AssetBundle> savedEnemy;List<string> savedEnemyNames;
  AudioClip[] savedBgm,savedSe;Dictionary<AudioSource,AudioClip> savedSources;Dictionary<AudioSource,bool> savedPlaying;
  AssetBundleLoader loader;GameManager manager;bool audioChanged;
  Dictionary<int,AssetBundle> BasicMap(){return (Dictionary<int,AssetBundle>)Get(loader,"m_BasicBundleList");}
  public AssetBundle[] Detach(string[] ids){
   savedBgm=null;savedSe=null;savedSources=null;savedPlaying=null;loader=Singleton<AssetBundleLoader>.Instance;manager=Singleton<GameManager>.Instance;
   savedBasic=new Dictionary<int,AssetBundle>();Dictionary<int,AssetBundle> current=BasicMap();
   foreach(KeyValuePair<int,AssetBundle> item in current)savedBasic.Add(item.Key,item.Value);
   List<AssetBundle> enemies=manager==null?null:(List<AssetBundle>)Get(manager,"m_EnemyAssetList");savedEnemy=enemies==null?null:new List<AssetBundle>();savedEnemyNames=new List<string>();if(enemies!=null)foreach(AssetBundle item in enemies){savedEnemy.Add(item);savedEnemyNames.Add(item==null?null:item.name);}
   List<AssetBundle> handles=new List<AssetBundle>();audioChanged=false;
   foreach(string id in ids){
    for(int i=0;i<AssetBundleDefine.m_BasicBundles.Length;i++)if(Id(AssetBundleDefine.m_BasicBundles[i])==id){AssetBundle bundle;if(current.TryGetValue(i,out bundle)){if(bundle!=null)handles.Add(bundle);current.Remove(i);}if(i==0)audioChanged=true;}
    if(enemies!=null)for(int i=enemies.Count-1;i>=0;i--)if(enemies[i]!=null&&enemies[i].name==id.Replace(".unity3d","")){handles.Add(enemies[i]);enemies.RemoveAt(i);}
   }
   return handles.ToArray();
  }
  public void Validate(string id,AssetBundle bundle){List<string> names;if(requiredPrefabs.TryGetValue(id,out names))foreach(string name in names)if(bundle.Load(name,typeof(GameObject))==null)throw new IOException("Updated resource missing required prefab: "+id+"/"+name);}
  public void Attach(Dictionary<string,WWW> prepared){
   Dictionary<int,AssetBundle> current=BasicMap();
   foreach(KeyValuePair<string,WWW> pair in prepared){
    if(!startup)for(int i=0;i<AssetBundleDefine.m_BasicBundles.Length;i++)if(Id(AssetBundleDefine.m_BasicBundles[i])==pair.Key)current[i]=pair.Value.assetBundle;
   }
   string audio=Id(AssetBundleDefine.m_BasicBundles[0]);WWW request;
   if(audioChanged&&prepared.TryGetValue(audio,out request))RefreshAudio(request.assetBundle);
  }
  void RefreshAudio(AssetBundle bundle){
   SoundManager sound=Singleton<SoundManager>.Instance;if(sound==null)return;
   savedBgm=(AudioClip[])Get(sound,"BGMAudioClips");savedSe=(AudioClip[])Get(sound,"SEAudioClips");savedSources=new Dictionary<AudioSource,AudioClip>();savedPlaying=new Dictionary<AudioSource,bool>();
   AudioClip[] bgm=new AudioClip[savedBgm.Length],se=new AudioClip[savedSe.Length];int[] indices=(int[])Get(sound,"m_BgmResIndex");
   for(int i=0;i<bgm.Length;i++)if(savedBgm[i]!=null){string name=AudioName("MakeBgmFileName",indices[i]);bgm[i]=bundle.Load(name,typeof(AudioClip)) as AudioClip;if(bgm[i]==null)throw new IOException("Missing updated BGM");}
   for(int i=0;i<se.Length;i++)if(savedSe[i]!=null){string name=AudioName("MakeSeFileName",i+1);se[i]=bundle.Load(name,typeof(AudioClip)) as AudioClip;if(se[i]==null)throw new IOException("Missing updated SE");}
   Set(sound,"BGMAudioClips",bgm);Set(sound,"SEAudioClips",se);
   ReplaceSources((AudioSource[])Get(sound,"BGMAudioSources"),savedBgm,bgm);ReplaceSources((AudioSource[])Get(sound,"SEAudioSources"),savedSe,se);
  }
  void ReplaceSources(AudioSource[] sources,AudioClip[] before,AudioClip[] after){
   if(sources==null)return;foreach(AudioSource source in sources)if(source!=null){AudioClip clip;if(!sourceClips.TryGetValue(source,out clip)||clip==null)continue;savedSources[source]=clip;savedPlaying[source]=source.isPlaying;
    for(int i=0;i<before.Length;i++)if(before[i]==clip&&after[i]!=null){bool playing=source.isPlaying;SetAudioClip(source,after[i]);if(playing)source.Play();break;}}
  }

  public void Restore(Dictionary<string,WWW> restored){
   if(savedBasic==null)return;Dictionary<int,AssetBundle> current=BasicMap();current.Clear();
   foreach(KeyValuePair<int,AssetBundle> pair in savedBasic){WWW request;string id=Id(AssetBundleDefine.m_BasicBundles[pair.Key]);current[pair.Key]=restored.TryGetValue(id,out request)?request.assetBundle:pair.Value;}
   if(manager!=null&&savedEnemy!=null){List<AssetBundle> enemies=new List<AssetBundle>();for(int i=0;i<savedEnemy.Count;i++){AssetBundle old=savedEnemy[i];AssetBundle value=old;foreach(KeyValuePair<string,WWW> item in restored)if(item.Key.Replace(".unity3d","")==savedEnemyNames[i]){value=item.Value.assetBundle;break;}enemies.Add(value);}Set(manager,"m_EnemyAssetList",enemies);}
   SoundManager sound=Singleton<SoundManager>.Instance;if(sound!=null&&savedBgm!=null){Set(sound,"BGMAudioClips",savedBgm);Set(sound,"SEAudioClips",savedSe);foreach(KeyValuePair<AudioSource,AudioClip> item in savedSources)if(item.Key!=null){SetAudioClip(item.Key,item.Value);if(savedPlaying[item.Key])item.Key.Play();}}
  }
  public void Accept(){savedBasic=null;savedEnemy=null;savedBgm=null;savedSe=null;savedSources=null;Resources.UnloadUnusedAssets();}
 }
}
public sealed class ResourceUpdateRunner:MonoBehaviour {}
