using System;
using System.Collections.Generic;
namespace RTD.ResourceUpdates {
 public sealed class LocalSnapshot {
  public string snapshotId;public string[] roots;public ResourceEntry[] resources;
 }
 public sealed class UpdatePlan {
  public ResolveResponse remote;public ResourceEntry[] downloads;
 }
 public static class ResourcePlanner {
  public static string[] ReloadClosure(ResourceEntry[] resources,string[] changed){
   Dictionary<string,bool> affected=new Dictionary<string,bool>();foreach(string id in changed)affected[id]=true;
   bool expanded=true;while(expanded){expanded=false;foreach(ResourceEntry entry in resources){if(affected.ContainsKey(entry.id))continue;foreach(string dependency in entry.dependencies)if(affected.ContainsKey(dependency)){affected[entry.id]=true;expanded=true;break;}}}
   List<string> result=new List<string>();foreach(ResourceEntry entry in resources)if(affected.ContainsKey(entry.id))result.Add(entry.id);return result.ToArray();
  }
  public static UpdatePlan Plan(ResolveResponse remote,LocalSnapshot local){
   Dictionary<string,ResourceEntry> known=new Dictionary<string,ResourceEntry>();
   if(local!=null&&local.resources!=null)foreach(ResourceEntry entry in local.resources)known[entry.id]=entry;
   List<ResourceEntry> downloads=new List<ResourceEntry>();
   foreach(ResourceEntry entry in remote.resources){ResourceEntry previous;
    if(!known.TryGetValue(entry.id,out previous)||previous.sha256!=entry.sha256||previous.size!=entry.size)downloads.Add(entry);
   }
   return new UpdatePlan{remote=remote,downloads=downloads.ToArray()};
  }
  // The caller must additionally confirm retained files/engine caches exist.
  public static bool CanFallback(LocalSnapshot local,string[] roots){
   if(local==null||local.resources==null)return false;
   Dictionary<string,ResourceEntry> known=new Dictionary<string,ResourceEntry>();
   foreach(ResourceEntry entry in local.resources){if(entry==null||known.ContainsKey(entry.id))return false;known.Add(entry.id,entry);}
   Dictionary<string,int> states=new Dictionary<string,int>();
   foreach(string id in roots)if(!Complete(id,known,states,0))return false;
   return true;
  }
  static bool Complete(string id,Dictionary<string,ResourceEntry> known,Dictionary<string,int> states,int depth){
   ResourceEntry entry;if(depth>128||!known.TryGetValue(id,out entry)||entry.dependencies==null)return false;
   int state;if(states.TryGetValue(id,out state))return state==2;
   states.Add(id,1);foreach(string dep in entry.dependencies)if(!Complete(dep,known,states,depth+1))return false;
   states[id]=2;return true;
  }
 }
}
