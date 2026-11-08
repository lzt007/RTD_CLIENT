using System;
using System.IO;
using System.Collections;
using RTD.ResourceUpdates;
class ResourceUpdateTransactionTests {
 sealed class Transport:IResourceTransport {
  readonly ResourceStore store;public bool corrupt;
  public Transport(ResourceStore store){this.store=store;}
  public IEnumerator Download(ResourceEntry entry){yield return null;store.PutVerified(entry.sha256,entry.size,System.Text.Encoding.UTF8.GetBytes(corrupt?"bad":"new"));}
 }
 sealed class Generation:IResourceGeneration {
  public bool fail;public bool disposeFailure;public bool activated;public bool restored;
  public IEnumerator Prepare(ResolveResponse release){return new Preparation(this);}
  sealed class Preparation:IEnumerator,IDisposable{readonly Generation owner;int step;public Preparation(Generation owner){this.owner=owner;}public object Current{get{return null;}}public bool MoveNext(){if(step++==0)return true;if(owner.fail)throw new IOException("invalid bundle");return false;}public void Reset(){throw new NotSupportedException();}public void Dispose(){if(owner.disposeFailure)throw new IOException("prepare cleanup failed");}}
  public void Activate(){activated=true;}
  public IEnumerator Restore(){restored=true;yield break;}
  public void ReleasePrevious(){}
 }
 static void Drain(IEnumerator routine){while(routine.MoveNext()){} }
 static void Main(){
  string root=Path.Combine(Path.GetTempPath(),"rtd-transaction-"+Guid.NewGuid().ToString("N"));
  try {
   ResourceStore store=new ResourceStore(root);string oldHash="cba06b5736faf67e54b07b561eae94395e774c517a7d910a54369e1263ccfbd4";
   store.PutVerified(oldHash,3,System.Text.Encoding.UTF8.GetBytes("old"));store.Stage("old",new string[]{oldHash},"old-manifest");store.Commit();store.Accept();
   ResolveResponse release=new ResolveResponse{snapshotId=new string('a',64),closureDigest=new string('b',64),protocol=1,resources=new ResourceEntry[]{new ResourceEntry{id="a.unity3d",sha256="11507a0e2f5e69d5dfa40a62a1bd7b6ee57e6bcd85c67c9b8431b36fff21c437",size=3,cacheVersion=1000000000,dependencies=new string[0]}}};
   Transport transport=new Transport(store);Generation generation=new Generation{fail=true};
   ResourceTransaction transaction=new ResourceTransaction(store,transport,generation,null);
   Drain(transaction.Execute(release,new string[]{"a.unity3d"}));
   if(transaction.Success||generation.activated||!generation.restored||store.Recover()!="old-manifest")throw new Exception("prepare failure must restore old generation");
   generation=new Generation();transport.corrupt=true;
   // Remove the previously verified download, forcing the corrupt path.
   File.Delete(store.BlobPath(release.resources[0].sha256));
   transaction=new ResourceTransaction(store,transport,generation,null);Drain(transaction.Execute(release,new string[]{"a.unity3d"}));
   if(transaction.Success||generation.activated||store.Recover()!="old-manifest")throw new Exception("bad download must not commit");
   transport.corrupt=false;transaction=new ResourceTransaction(store,transport,generation,null);Drain(transaction.Execute(release,new string[]{"a.unity3d"}));
   if(!transaction.Success||!generation.activated||!store.Recover().Contains("a.unity3d"))throw new Exception("complete generation activates");
   generation=new Generation();transaction=new ResourceTransaction(store,transport,generation,null);bool cancel=false;transaction.Cancelled=delegate{return cancel;};
   IEnumerator cancelled=transaction.Execute(release,new string[]{"a.unity3d"});if(!cancelled.MoveNext())throw new Exception("Expected preparation yield");cancel=true;Drain(cancelled);
   if(transaction.Success||!generation.restored||transaction.Failure==null)throw new Exception("cancel must restore whole generation");
   generation=new Generation{disposeFailure=true};transaction=new ResourceTransaction(store,transport,generation,null);Drain(transaction.Execute(release,new string[]{"a.unity3d"}));
   if(transaction.Success||!generation.restored)throw new Exception("preparation disposal failure must roll back");
   Console.WriteLine("PASS 5 transaction integration checks");
  } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
