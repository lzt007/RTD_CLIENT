using System;
using System.Collections.Generic;
using LitJson;
namespace RTD.ResourceUpdates {
 public sealed class ResourceEntry {
  public string id;public string sha256;public int size;public int cacheVersion;public string downloadPath;public string[] dependencies;
 }
 public sealed class ResolveResponse {
  public int protocol;public string snapshotId;public string closureDigest;public ResourceEntry[] resources;
 }
 public sealed class ResolveRequest {
  public int protocol=1;public string operationId;public string[] roots;public string knownSnapshot;
 }
 public static class ResourceProtocol {
  static void Require(bool value){if(!value)throw new FormatException("Invalid resource snapshot");}
  public static bool IsHash(string value){if(value==null||value.Length!=64)return false;foreach(char c in value)if(!((c>='a'&&c<='f')||(c>='0'&&c<='9')))return false;return true;}
  public static bool IsId(string value){
   if(String.IsNullOrEmpty(value)||value.Length>256||!value.EndsWith(".unity3d")||value.StartsWith("/"))return false;
   foreach(string part in value.Split('/'))if(part==""||part=="."||part=="..")return false;
   foreach(char c in value)if(!((c>='a'&&c<='z')||(c>='A'&&c<='Z')||(c>='0'&&c<='9')||c=='/'||c=='_'||c=='.'||c=='-'))return false;
   return true;
  }
  public static ResolveResponse Parse(string json,string[] roots){
   Require(json!=null&&json.Length<=4194304);
   ResolveResponse result=JsonMapper.ToObject<ResolveResponse>(json);
   Require(result!=null&&result.protocol==1&&IsHash(result.snapshotId)&&IsHash(result.closureDigest)&&result.resources!=null&&result.resources.Length<=4096);
   Dictionary<string,ResourceEntry> entries=new Dictionary<string,ResourceEntry>();
   Dictionary<string,int> versions=new Dictionary<string,int>();
   foreach(ResourceEntry entry in result.resources){
    Require(entry!=null&&IsId(entry.id)&&IsHash(entry.sha256)&&entry.size>0&&entry.size<=536870912&&entry.cacheVersion>0&&entry.dependencies!=null&&!entries.ContainsKey(entry.id));
    string name=entry.id.Substring(entry.id.LastIndexOf('/')+1);
    Require(entry.downloadPath=="/resources/v1/blobs/"+entry.sha256+"/"+name);
    string cacheKey=name+"@"+entry.cacheVersion;
    Require(!versions.ContainsKey(cacheKey));versions.Add(cacheKey,entry.cacheVersion);entries.Add(entry.id,entry);
   }
   Dictionary<string,int> states=new Dictionary<string,int>();
   foreach(string root in roots){Require(entries.ContainsKey(root));Visit(root,entries,states,0);}
   // Validate even surplus entries: malformed dependency graphs never enter storage.
   foreach(string id in entries.Keys)Visit(id,entries,states,0);
   return result;
  }
  static void Visit(string id,Dictionary<string,ResourceEntry> entries,Dictionary<string,int> states,int depth){
   Require(depth<=128&&entries.ContainsKey(id));int state;
   if(states.TryGetValue(id,out state)){Require(state==2);return;}
   states.Add(id,1);foreach(string dep in entries[id].dependencies)Visit(dep,entries,states,depth+1);states[id]=2;
  }
 }
}
