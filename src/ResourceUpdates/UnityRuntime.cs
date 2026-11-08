using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using LitJson;
using UnityEngine;
namespace RTD.ResourceUpdates {
 public sealed class BaselineEntry {public string id;public string sha256;public int legacyVersion;public string legacyPayload;public string legacyPayloadHash;public int legacyLength;public int size;}
 public sealed class ResourceOperation {public string[] CheckedResources;public bool Cancelled;public bool Ready;public string Error;public string Snapshot;}
 public interface IResourceGameHooks {
  AssetBundle[] Detach(string[] ids);
  void Attach(Dictionary<string,WWW> prepared);
  void Validate(string id,AssetBundle bundle);
  void Accept();
  void Restore(Dictionary<string,WWW> restored);
 }
 public static class ResourceRuntime {
  public static IResourceGameHooks Game;
  public static bool Busy;
  static ResourceStore store;
  static string cacheRoot;
  static Dictionary<string,BaselineEntry> baseline=new Dictionary<string,BaselineEntry>();
  static Dictionary<string,ResourceEntry> active=new Dictionary<string,ResourceEntry>();
  static Dictionary<string,UnityEngine.Object[]> validated=new Dictionary<string,UnityEngine.Object[]>();
  static Dictionary<string,WWW> loaded=new Dictionary<string,WWW>();
  static Dictionary<string,bool> verified=new Dictionary<string,bool>();
  public static string BaseUrl {get{return ResourceUpdateConfig.BaseUrl;}}
  public static void Log(string message){Debug.Log("RTD_RESOURCE_UPDATE "+message);}
  static void Init(){
   if(store!=null)return;
   string root;
   using(AndroidJavaClass player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
   using(AndroidJavaObject activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
   using(AndroidJavaObject files=activity.Call<AndroidJavaObject>("getFilesDir"))root=files.Call<string>("getAbsolutePath");
   cacheRoot=Path.Combine(Application.persistentDataPath,"UnityCache");
   ResourceStore candidate=new ResourceStore(Path.Combine(root,"rtd-resource-store"));
   string local=candidate.Recover();
   BaselineEntry[] entries=JsonMapper.ToObject<BaselineEntry[]>(ResourceBaselineIndex.Json);
   foreach(BaselineEntry entry in entries)baseline[entry.id]=entry;
   store=candidate;
   if(local!=null){LocalSnapshot snapshot=ReadLocal(local);if(snapshot!=null)foreach(ResourceEntry entry in snapshot.resources)active[entry.id]=entry;}
   Log("store_ready baseline="+baseline.Count);
  }
  static LocalSnapshot ReadLocal(string text){
   try {
    LocalSnapshot local=JsonMapper.ToObject<LocalSnapshot>(text);
    if(local==null||local.roots==null||local.resources==null)return null;
    ResourceProtocol.Parse(JsonMapper.ToJson(new ResolveResponse{protocol=1,snapshotId=local.snapshotId,closureDigest=new string('0',64),resources=local.resources}),local.roots);
    return local;
   }catch(Exception){return null;}
  }
  static bool Embedded(ResourceEntry entry){
   BaselineEntry original;
   if(!baseline.TryGetValue(entry.id,out original)||original.sha256!=entry.sha256)return false;
   string path=Path.Combine(cacheRoot,original.legacyPayload);
   return File.Exists(path)&&File.Exists(path+".rtd-"+original.legacyPayloadHash)&&ResourceFiles.Length(path)==original.legacyLength&&Caching.IsVersionCached(BaseUrl+"/"+entry.id,original.legacyVersion);
  }
  static bool Available(ResourceEntry entry){
   if(Embedded(entry))return true;
   string path=store.BlobPath(entry.sha256);bool known;
   if(verified.TryGetValue(entry.sha256,out known)&&known)return File.Exists(path)&&ResourceFiles.Length(path)==entry.size;
   bool valid=store.HasVerified(entry.sha256,entry.size);if(valid)verified[entry.sha256]=true;return valid;
  }
  static string Match(string url){
   List<string> ids=new List<string>();foreach(string id in active.Keys)ids.Add(id);return ResourceProtocol.MatchId(url,ids.ToArray());
  }

  public static bool IsVersionCached(string url,int version){
   if(Busy)return false;
   string id=Match(url);return id==null?Caching.IsVersionCached(url,version):Available(active[id]);
  }
  static WWW Open(ResourceEntry entry){
   if(Embedded(entry)){BaselineEntry original=baseline[entry.id];return WWW.LoadFromCacheOrDownload(BaseUrl+"/"+entry.id,original.legacyVersion);}
   if(!Available(entry))throw new IOException("Resource not verified: "+entry.id);
   return new WWW("file://"+store.BlobPath(entry.sha256));
  }
  public static WWW LoadFromCacheOrDownload(string url,int version){
   string id=Match(url);if(id==null)throw new IOException("Resource outside checked operation");
   WWW value;if(loaded.TryGetValue(id,out value)&&value!=null){try{if(value.isDone&&value.assetBundle!=null)return value;}catch(Exception){}loaded.Remove(id);}
   value=Open(active[id]);loaded[id]=value;return value;
  }
  sealed class RunResult {public Exception Error;}
  static IEnumerator TryRun(IEnumerator routine,RunResult result){
   try {while(true){bool moved=false;object current=null;try{moved=routine.MoveNext();if(moved)current=routine.Current;}catch(Exception error){result.Error=error;}
    if(result.Error!=null||!moved)yield break;yield return current;}}
   finally{IDisposable item=routine as IDisposable;if(item!=null)item.Dispose();}
  }
  static IEnumerator Resolve(string[] roots,string operation,ResolveResponse[] output){
   Hashtable headers=new Hashtable();headers.Add("Content-Type","application/json");
   byte[] body=Encoding.UTF8.GetBytes(JsonMapper.ToJson(new ResolveRequest{operationId=operation,roots=roots}));
   WWW request=new WWW(BaseUrl+"/resources/v1/resolve",body,headers);float started=Time.realtimeSinceStartup;
   try{
    while(!request.isDone){if(Time.realtimeSinceStartup-started>=2f)throw new IOException("Version check timeout");yield return null;}
    if(!String.IsNullOrEmpty(request.error))throw new IOException("Version check failed");
    output[0]=ResourceProtocol.Parse(request.text,roots);
    if(Time.realtimeSinceStartup-started>=2f)throw new IOException("Version check processing timeout");
    Log("resolved operation="+operation+" snapshot="+output[0].snapshotId+" count="+output[0].resources.Length+" ms="+((Time.realtimeSinceStartup-started)*1000f));
   }finally{request.Dispose();}
  }
  static LocalSnapshot Fallback(string[] roots){
   List<LocalSnapshot> candidates=new List<LocalSnapshot>();
   foreach(string text in store.ReadHistory()){LocalSnapshot item=ReadLocal(text);if(item!=null)candidates.Add(item);}
   List<ResourceEntry> originals=new List<ResourceEntry>();
   foreach(string id in roots){BaselineEntry b;if(!baseline.TryGetValue(id,out b)){originals.Clear();break;}originals.Add(new ResourceEntry{id=id,sha256=b.sha256,size=b.size,cacheVersion=b.legacyVersion,downloadPath="/resources/v1/blobs/"+b.sha256+"/"+id.Substring(id.LastIndexOf('/')+1),dependencies=new string[0]});}
   if(originals.Count==roots.Length)candidates.Add(new LocalSnapshot{snapshotId=new string('0',64),roots=roots,resources=originals.ToArray()});
   foreach(LocalSnapshot candidate in candidates){if(!ResourcePlanner.CanFallback(candidate,roots))continue;bool complete=true;foreach(ResourceEntry entry in candidate.resources)if(!Available(entry)){complete=false;break;}if(complete)return candidate;}
   return null;
  }
  static string[] ResourceIds(ResourceEntry[] entries){string[] ids=new string[entries.Length];for(int i=0;i<ids.Length;i++)ids[i]=entries[i].id;return ids;}
  public static IEnumerator EnsureCurrent(string[] roots,ResourceOperation operation){
   while(Busy)yield return null;Busy=true;
   try {
    Exception initError=null;try{Init();}catch(Exception error){initError=error;}
    if(initError!=null){operation.Error="Resource store unavailable: "+initError.GetType().Name;Log(operation.Error);yield break;}
    string id=Guid.NewGuid().ToString("N");ResolveResponse[] response=new ResolveResponse[1];RunResult resolve=new RunResult();
    IEnumerator request=TryRun(Resolve(roots,id,response),resolve);while(request.MoveNext())yield return request.Current;
    if(resolve.Error==null){
     ResourceTransaction transaction=new ResourceTransaction(store,new UnityTransport(store),new UnityGeneration(response[0]),Available);transaction.Cancelled=delegate{return operation.Cancelled;};
     IEnumerator work=transaction.Execute(response[0],roots);while(work.MoveNext())yield return work.Current;
     if(transaction.Success){operation.CheckedResources=ResourceIds(response[0].resources);operation.Ready=true;operation.Snapshot=response[0].snapshotId;Log("committed operation="+id+" snapshot="+operation.Snapshot);yield break;}
     Log("rollback operation="+id+" reason="+transaction.Failure.GetType().Name+" restore="+(transaction.RestoreFailure==null));
     if(transaction.RestoreFailure!=null){operation.Error="Resource restore failed";yield break;}
    }else Log("resolve_failed operation="+id+" reason="+resolve.Error.GetType().Name);
    if(operation.Cancelled){operation.Error="Resource operation cancelled";yield break;}
    LocalSnapshot previous=null;try{previous=Fallback(roots);}catch(Exception error){Log("history_unavailable reason="+error.GetType().Name);}
    if(previous==null){operation.Error="No complete resource generation";Log("unavailable operation="+id);yield break;}
    ResolveResponse fallback=new ResolveResponse{protocol=1,snapshotId=previous.snapshotId,closureDigest=new string('0',64),resources=previous.resources};
    ResourceTransaction restoreTransaction=new ResourceTransaction(store,new UnityTransport(store),new UnityGeneration(fallback),Available);
    IEnumerator restoreWork=restoreTransaction.Execute(fallback,roots);while(restoreWork.MoveNext())yield return restoreWork.Current;
    operation.CheckedResources=ResourceIds(previous.resources);operation.Ready=restoreTransaction.Success;operation.Snapshot=previous.snapshotId;
    if(!operation.Ready)operation.Error="Previous resource generation unavailable";
    Log("fallback operation="+id+" snapshot="+previous.snapshotId+" ready="+operation.Ready);
   }finally{Busy=false;}
  }
  sealed class UnityTransport:IResourceBatchTransport {
   readonly ResourceStore store;public UnityTransport(ResourceStore value){store=value;}
   public IEnumerator DownloadBatch(ResourceEntry[] entries){
    IEnumerator[] work=new IEnumerator[2];int next=0;
    try{while(next<entries.Length||work[0]!=null||work[1]!=null){
     for(int i=0;i<2;i++){if(work[i]==null&&next<entries.Length)work[i]=Download(entries[next++]);
      if(work[i]!=null&&!work[i].MoveNext()){IDisposable done=work[i] as IDisposable;if(done!=null)done.Dispose();work[i]=null;}}
     yield return null;
    }}finally{foreach(IEnumerator item in work){IDisposable disposable=item as IDisposable;if(disposable!=null)disposable.Dispose();}}
   }
   public IEnumerator Download(ResourceEntry entry){
    Log("download_begin id="+entry.id+" version="+entry.cacheVersion+" bytes="+entry.size);
    WWW request=new WWW(BaseUrl+entry.downloadPath);TransferBudget budget=new TransferBudget(Time.realtimeSinceStartup,15);
    try {
     while(!request.isDone){budget.Observe(request.progress,Time.realtimeSinceStartup);if(budget.Expired(Time.realtimeSinceStartup))throw new IOException("Resource download stalled");yield return null;}
     if(!String.IsNullOrEmpty(request.error))throw new IOException("Resource download failed: "+entry.id);
     store.PutVerified(entry.sha256,entry.size,request.bytes);verified[entry.sha256]=true;
     Log("downloaded id="+entry.id+" version="+entry.cacheVersion+" bytes="+entry.size);
    }finally{request.Dispose();}
   }
  }
  sealed class UnityGeneration:IResourceGeneration {
   readonly ResolveResponse next;Dictionary<string,ResourceEntry> before;Dictionary<string,WWW> prepared=new Dictionary<string,WWW>();
   List<ResourceEntry> changed=new List<ResourceEntry>();bool began;Dictionary<string,UnityEngine.Object[]> beforeObjects;Dictionary<string,UnityEngine.Object[]> preparedObjects=new Dictionary<string,UnityEngine.Object[]>();
   public UnityGeneration(ResolveResponse release){next=release;}
   public IEnumerator Prepare(ResolveResponse release){
    beforeObjects=new Dictionary<string,UnityEngine.Object[]>();foreach(KeyValuePair<string,UnityEngine.Object[]> pair in validated)beforeObjects.Add(pair.Key,pair.Value);
    before=new Dictionary<string,ResourceEntry>();foreach(KeyValuePair<string,ResourceEntry> pair in active)before.Add(pair.Key,pair.Value);
    Dictionary<string,bool> affected=new Dictionary<string,bool>();
    foreach(ResourceEntry entry in next.resources){ResourceEntry old;
     if(active.TryGetValue(entry.id,out old)&&old.sha256==entry.sha256)continue;
     if(!active.ContainsKey(entry.id)&&Embedded(entry))continue;
     affected[entry.id]=true;
    }
    // Include cached parents outside this operation: a later load must not reuse
    // a parent archive whose objects still reference an old shared dependency.
    Dictionary<string,ResourceEntry> union=new Dictionary<string,ResourceEntry>();foreach(ResourceEntry entry in before.Values)union[entry.id]=entry;foreach(ResourceEntry entry in next.resources)union[entry.id]=entry;
    List<ResourceEntry> graph=new List<ResourceEntry>();foreach(ResourceEntry entry in union.Values)graph.Add(entry);
    List<string> seeds=new List<string>();foreach(string id in affected.Keys)seeds.Add(id);
    foreach(string id in ResourcePlanner.ReloadClosure(graph.ToArray(),seeds.ToArray()))changed.Add(union[id]);
    string[] ids=new string[changed.Count];for(int i=0;i<ids.Length;i++)ids[i]=changed[i].id;
    began=true;AssetBundle[] handles=Game==null?new AssetBundle[0]:Game.Detach(ids);
    List<AssetBundle> unloaded=new List<AssetBundle>();
    foreach(AssetBundle handle in handles)if(handle!=null&&!unloaded.Contains(handle)){handle.Unload(false);unloaded.Add(handle);}
    foreach(ResourceEntry entry in changed){WWW old;if(loaded.TryGetValue(entry.id,out old)){try{AssetBundle bundle=old.assetBundle;if(bundle!=null&&!unloaded.Contains(bundle))bundle.Unload(false);}catch(Exception){}loaded.Remove(entry.id);}}
    foreach(ResourceEntry entry in changed){
     Log("prepare_begin id="+entry.id+" version="+entry.cacheVersion);WWW request=Open(entry);prepared.Add(entry.id,request);
     while(!request.isDone)yield return null;
     if(!String.IsNullOrEmpty(request.error)||request.assetBundle==null)throw new IOException("Resource bundle preparation failed: "+entry.id);
     request.assetBundle.name=entry.id.Substring(entry.id.LastIndexOf('/')+1).Replace(".unity3d","");
    }
    foreach(ResourceEntry entry in changed){WWW item=prepared[entry.id];if(Game!=null)Game.Validate(entry.id,item.assetBundle);
     if(entry.requiredPrefabs==null)continue;List<UnityEngine.Object> objects=new List<UnityEngine.Object>();
     foreach(string name in entry.requiredPrefabs){UnityEngine.Object value=item.assetBundle.Load(name,typeof(GameObject));if(value==null)throw new IOException("Missing required prefab export: "+entry.id+"/"+name);objects.Add(value);Log("prefab_verified id="+entry.id+" export="+name+" object="+value.name+" instance="+value.GetInstanceID());}
     preparedObjects[entry.id]=objects.ToArray();
    }
   }
   public void Activate(){
    Log("memory_activate count="+prepared.Count);
    if(Game!=null)Game.Attach(prepared);
    foreach(ResourceEntry entry in next.resources)active[entry.id]=entry;
    foreach(KeyValuePair<string,WWW> item in prepared)loaded[item.Key]=item.Value;foreach(KeyValuePair<string,UnityEngine.Object[]> item in preparedObjects)validated[item.Key]=item.Value;
   }
   public IEnumerator Restore(){
    Log("memory_restore count="+changed.Count);
    if(!began)yield break;
    foreach(WWW item in prepared.Values){try{if(item.assetBundle!=null)item.assetBundle.Unload(true);}catch(Exception){}item.Dispose();}
    Dictionary<string,WWW> restored=new Dictionary<string,WWW>();
    foreach(ResourceEntry entry in changed){ResourceEntry old;if(!before.TryGetValue(entry.id,out old)){loaded.Remove(entry.id);continue;}
     WWW request=Open(old);restored.Add(entry.id,request);while(!request.isDone)yield return null;
     if(!String.IsNullOrEmpty(request.error)||request.assetBundle==null)throw new IOException("Previous bundle reload failed");
     request.assetBundle.name=old.id.Replace(".unity3d","");loaded[entry.id]=request;
    }
    if(Game!=null)Game.Restore(restored);active=before;validated=beforeObjects;
   }
   public void ReleasePrevious(){if(Game!=null)Game.Accept();}
  }
 }
}
