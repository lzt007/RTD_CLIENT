using System;
using System.IO;
using UnityEngine;

public static class RTDEmbeddedCache {
 static bool ready;
 static bool assertCache;
 public static bool IsVersionCached(string url,int version) {
  bool cached=Caching.IsVersionCached(url,version);
  Debug.Log("RTD_CACHE_CHECK hit="+cached+" version="+version+" url="+url);
  return cached;
 }
 public static WWW LoadFromCacheOrDownload(string url,int version) {
  bool cached=Caching.IsVersionCached(url,version);
  Debug.Log("RTD_CACHE_LOAD hit="+cached+" version="+version+" url="+url);
  if(assertCache&&!cached)throw new IOException("Offline cache verification failed: "+url);
  return WWW.LoadFromCacheOrDownload(url,version);
 }
 static void RefreshMetadata(string path,string timestamp) {
  byte[] bytes=File.ReadAllBytes(path);
  string[] lines=System.Text.Encoding.UTF8.GetString(bytes,0,bytes.Length).Split('\n');
  if(lines.Length<4)throw new IOException("Invalid embedded Unity cache metadata: "+path);
  // Unity stores last-use Unix time on line 2 and expires the 2014 backup otherwise.
  lines[1]=timestamp;
  bytes=System.Text.Encoding.UTF8.GetBytes(String.Join("\n",lines));
  using(Stream output=File.Create(path))output.Write(bytes,0,bytes.Length);
 }

 public static void Ensure() {
  if(ready)return;
  using(AndroidJavaClass player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
  using(AndroidJavaObject activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
    using(AndroidJavaObject assets=activity.Call<AndroidJavaObject>("getAssets"))
  using(AndroidJavaClass channels=new AndroidJavaClass("java.nio.channels.Channels")) {
   string root=Path.Combine(Application.persistentDataPath,"UnityCache");
   string maintenance;
   using(AndroidJavaObject files=activity.Call<AndroidJavaObject>("getFilesDir"))maintenance=files.Call<string>("getAbsolutePath");
   string resetFlag=Path.Combine(maintenance,"rtd-reseed-cache");
   bool reset=File.Exists(resetFlag);assertCache=File.Exists(Path.Combine(maintenance,"rtd-assert-cache"));
   int copiedFiles=0,skippedFiles=0;
   if(reset)foreach(string entry in EmbeddedCacheIndex.Files) {
    string[] fields=entry.Split('|');string target=Path.Combine(root,fields[0]);
    if(File.Exists(target))File.Delete(target);
    string marker=target+".rtd-"+fields[2];if(File.Exists(marker))File.Delete(marker);
   }
   using(AndroidJavaClass clock=new AndroidJavaClass("java.lang.System")) {
   string timestamp=(clock.CallStatic<long>("currentTimeMillis")/1000L).ToString();
   foreach(string entry in EmbeddedCacheIndex.Files) {
    string[] fields=entry.Split('|');string relative=fields[0];long size=Int64.Parse(fields[1]);
    string target=Path.Combine(root,relative);string marker=target+".rtd-"+fields[2];
    bool metadata=relative.EndsWith("/__info");
    if(!metadata&&File.Exists(target)&&File.Exists(marker))using(Stream existing=File.OpenRead(target)){if(existing.Length==size){skippedFiles++;continue;}}
    if(metadata&&File.Exists(target)&&File.Exists(marker)) {
     RefreshMetadata(target,timestamp);skippedFiles++;continue;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    if(File.Exists(marker))File.Delete(marker);
    using(AndroidJavaObject input=assets.Call<AndroidJavaObject>("open","rtd-cache/"+relative))
    using(AndroidJavaObject source=channels.CallStatic<AndroidJavaObject>("newChannel",input))
    using(AndroidJavaObject output=new AndroidJavaObject("java.io.FileOutputStream",target))
    using(AndroidJavaObject destination=output.Call<AndroidJavaObject>("getChannel")) {
     try {
      long position=0;
      while(position<size) {
       long copied=destination.Call<long>("transferFrom",source,position,(size-position>1048576L?1048576L:size-position));
       if(copied<=0)throw new IOException("Incomplete embedded cache: "+relative);
       position+=copied;
      }
      destination.Call("force",true);
     } finally {destination.Call("close");source.Call("close");output.Call("close");input.Call("close");}
    }
    if(metadata)RefreshMetadata(target,timestamp);
    using(Stream completed=File.Create(marker)){}
    copiedFiles++;
   }
   }
   if(reset)File.Delete(resetFlag);
   ready=true;Debug.Log("RTD_EMBEDDED_CACHE ready: "+root+" copied="+copiedFiles+" skipped="+skippedFiles+" reseed="+reset+" assertCache="+assertCache);
  }
 }
}

