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
  Console.WriteLine("PASS 9 protocol validation checks");
 }
}
