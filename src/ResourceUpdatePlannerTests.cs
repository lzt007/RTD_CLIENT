using System;
using RTD.ResourceUpdates;
class ResourceUpdatePlannerTests {
 static ResourceEntry E(string id,string hash,params string[] deps){return new ResourceEntry{id=id,sha256=hash,size=3,cacheVersion=1,dependencies=deps};}
 static void Main(){
  ResourceEntry a=E("a.unity3d",new string('a',64),"b.unity3d"),b=E("b.unity3d",new string('b',64));
  LocalSnapshot local=new LocalSnapshot{snapshotId="old",resources=new ResourceEntry[]{a,b},roots=new string[]{"a.unity3d"}};
  ResolveResponse same=new ResolveResponse{snapshotId="same",resources=new ResourceEntry[]{a,b}};
  if(ResourcePlanner.Plan(same,local).downloads.Length!=0)throw new Exception("unchanged downloads");
  ResourceEntry fresh=E("a.unity3d",new string('c',64),"b.unity3d");
  UpdatePlan plan=ResourcePlanner.Plan(new ResolveResponse{snapshotId="new",resources=new ResourceEntry[]{fresh,b}},local);
  if(plan.downloads.Length!=1||plan.downloads[0].id!="a.unity3d")throw new Exception("wrong differential");
  if(!ResourcePlanner.CanFallback(local,new string[]{"a.unity3d"}))throw new Exception("complete fallback rejected");
  if(ResourcePlanner.CanFallback(new LocalSnapshot{resources=new ResourceEntry[]{a}},new string[]{"a.unity3d"}))throw new Exception("missing dependency accepted");
  if(ResourcePlanner.CanFallback(null,new string[]{"a.unity3d"}))throw new Exception("first load fallback");
  if(ResourcePlanner.CanFallback(local,new string[]{"other.unity3d"}))throw new Exception("unrelated fallback");
  Console.WriteLine("PASS 6 planner checks");
 }
}
