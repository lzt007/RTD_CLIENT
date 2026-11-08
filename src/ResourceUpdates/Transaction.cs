using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using LitJson;
namespace RTD.ResourceUpdates {
 public interface IResourceTransport {IEnumerator Download(ResourceEntry entry);}
 public interface IResourceGeneration {
  IEnumerator Prepare(ResolveResponse release);void Activate();IEnumerator Restore();void ReleasePrevious();
 }
 public delegate bool IsEmbeddedResource(ResourceEntry entry);
 public sealed class ResourceTransaction {
  readonly ResourceStore store;readonly IResourceTransport transport;readonly IResourceGeneration generation;readonly IsEmbeddedResource embedded;
  public bool Success;public Exception Failure;public Exception RestoreFailure;public string Warning;
  public ResourceTransaction(ResourceStore store,IResourceTransport transport,IResourceGeneration generation,IsEmbeddedResource embedded){this.store=store;this.transport=transport;this.generation=generation;this.embedded=embedded;}
  public IEnumerator Execute(ResolveResponse release,string[] roots){
   Success=false;Failure=null;RestoreFailure=null;
   IEnumerator forward=Drive(Forward(release,roots));while(forward.MoveNext())yield return forward.Current;
   if(Failure!=null){
    Exception original=Failure;Failure=null;
    IEnumerator restore=Drive(generation.Restore());while(restore.MoveNext())yield return restore.Current;
    RestoreFailure=Failure;Failure=original;
    try{store.Rollback();}catch(Exception error){RestoreFailure=error;}
    yield break;
   }
   Success=true;try{generation.ReleasePrevious();}catch(Exception error){Warning="Resource cleanup: "+error.GetType().Name;}
  }
  IEnumerator Forward(ResolveResponse release,string[] roots){
   List<string> hashes=new List<string>();
   foreach(ResourceEntry entry in release.resources){
    if(embedded!=null&&embedded(entry))continue;
    if(!store.HasVerified(entry.sha256,entry.size))yield return transport.Download(entry);
    if(!store.HasVerified(entry.sha256,entry.size))throw new IOException("Incomplete resource download");
    hashes.Add(entry.sha256);
   }
   LocalSnapshot next=new LocalSnapshot{snapshotId=release.snapshotId,roots=roots,resources=release.resources};
   store.Stage(release.snapshotId,hashes.ToArray(),JsonMapper.ToJson(next));
   yield return generation.Prepare(release);
   store.Commit();generation.Activate();store.Accept();Warning=store.Warning;
  }
  IEnumerator Drive(IEnumerator start){
   Stack<IEnumerator> stack=new Stack<IEnumerator>();stack.Push(start);
   try {
    while(stack.Count>0){
     IEnumerator routine=stack.Peek();bool moved=false;object current=null;
     try{moved=routine.MoveNext();if(moved)current=routine.Current;}catch(Exception error){Failure=error;yield break;}
     if(!moved){stack.Pop();IDisposable disposable=routine as IDisposable;if(disposable!=null)disposable.Dispose();continue;}
     IEnumerator nested=current as IEnumerator;if(nested!=null){stack.Push(nested);continue;}
     yield return current;
    }
   }finally{while(stack.Count>0){IDisposable item=stack.Pop() as IDisposable;if(item!=null)try{item.Dispose();}catch(Exception error){if(Failure==null)Failure=error;}}}
  }
 }
}
