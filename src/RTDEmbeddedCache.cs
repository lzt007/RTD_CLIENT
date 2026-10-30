using System;
using System.IO;
using UnityEngine;

public static class RTDEmbeddedCache {
 static bool ready;
 public static void Ensure() {
  if(ready)return;
  using(AndroidJavaClass player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
  using(AndroidJavaObject activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
  using(AndroidJavaObject cache=activity.Call<AndroidJavaObject>("getCacheDir"))
  using(AndroidJavaObject assets=activity.Call<AndroidJavaObject>("getAssets"))
  using(AndroidJavaClass channels=new AndroidJavaClass("java.nio.channels.Channels")) {
   string root=Path.Combine(cache.Call<string>("getAbsolutePath"),"UnityCache");
   foreach(string entry in EmbeddedCacheIndex.Files) {
    string[] fields=entry.Split('|');string relative=fields[0];long size=Int64.Parse(fields[1]);
    string target=Path.Combine(root,relative);string marker=target+".rtd-"+fields[2];
    if(File.Exists(target)&&File.Exists(marker))using(Stream existing=File.OpenRead(target)){if(existing.Length==size)continue;}
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
    using(Stream completed=File.Create(marker)){}
   }
   ready=true;Debug.Log("RTD_EMBEDDED_CACHE ready: "+root);
  }
 }
}

