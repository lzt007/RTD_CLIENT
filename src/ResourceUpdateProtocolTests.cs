using System;
using RTD.ResourceUpdates;
class ResourceUpdateProtocolTests {
 static void Main(){
  string hash=new string('a',64);
  string entry="{\"id\":\"a.unity3d\",\"sha256\":\""+hash+"\",\"size\":3,\"cacheVersion\":1,\"downloadPath\":\"/resources/v1/blobs/"+hash+"/a.unity3d\",\"dependencies\":[]}";
  string json="{\"protocol\":1,\"snapshotId\":\""+hash+"\",\"closureDigest\":\""+hash+"\",\"resources\":["+entry+"]}";
  ResolveResponse r=ResourceProtocol.Parse(json,new string[]{"a.unity3d"});
  if(r.resources.Length!=1||r.resources[0].cacheVersion!=1)throw new Exception("valid response");
  string[] invalid={json.Replace("\"protocol\":1","\"protocol\":2"),json.Replace("\"size\":3","\"size\":-1"),json.Replace("\"dependencies\":[]","\"dependencies\":[\"missing.unity3d\"]"),json.Replace("\"cacheVersion\":1","\"cacheVersion\":0"),json.Replace("\"resources\":["+entry+"]","\"resources\":["+entry+","+entry+"]"),json.Replace("/resources/v1/blobs/","http://localhost/"),json.Replace("\"dependencies\":[]","\"dependencies\":[\"a.unity3d\"]")};
  foreach(string bad in invalid){bool rejected=false;try{ResourceProtocol.Parse(bad,new string[]{"a.unity3d"});}catch(Exception){rejected=true;}if(!rejected)throw new Exception("accepted invalid response");}
  bool missing=false;try{ResourceProtocol.Parse(json,new string[]{"other.unity3d"});}catch(Exception){missing=true;}if(!missing)throw new Exception("missing root");
  string[] ids={"x.unity3d","one/x.unity3d","two/x.unity3d"};
  if(ResourceProtocol.MatchId("http://localhost/Android/one/x.unity3d?v=1",ids)!="one/x.unity3d")throw new Exception("same filename different path merged");
  if(ResourceProtocol.MatchId("http://localhost/Android/two/x.unity3d",ids)!="two/x.unity3d")throw new Exception("wrong full logical path");
  if(ResourceProtocol.MatchId("http://localhost/Android/y.unity3d",ids)!=null)throw new Exception("unknown path matched");
  string contract=json.Replace("\"dependencies\":[]","\"dependencies\":[],\"requiredPrefabs\":[\"ast_probe\"]");
  if(ResourceProtocol.Parse(contract,new string[]{"a.unity3d"}).resources[0].requiredPrefabs[0]!="ast_probe")throw new Exception("missing prefab export contract");
  bool badContract=false;try{ResourceProtocol.Parse(contract.Replace("ast_probe","bad\\nname"),new string[]{"a.unity3d"});}catch(Exception){badContract=true;}
  if(!badContract)throw new Exception("control character accepted in export contract");
  Console.WriteLine("PASS 14 protocol validation checks");
 }
}
