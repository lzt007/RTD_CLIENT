using System;
using System.IO;
using System.Text;
namespace RTD.ResourceUpdates {
 // Files are immutable; generation directories are renamed on the same volume.
 // The journal remains until memory activation has been accepted.
 public sealed class ResourceStore {
  readonly string root;
  public string Warning;
  public ResourceStore(string root){this.root=root;Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(root,"blobs"));}
  string At(string name){return Path.Combine(root,name);}
  static void Remove(string path){if(Directory.Exists(path))ResourceFiles.RemoveDirectory(path);}
  static void Write(string path,string value){string pending=path+".pending";WriteBytes(pending,Encoding.UTF8.GetBytes(value));ResourceFiles.Move(pending,path);}
  static void WriteBytes(string path,byte[] value){using(FileStream f=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(value,0,value.Length);f.Flush();}ResourceFiles.SyncFile(path);ResourceFiles.SyncDirectory(Path.GetDirectoryName(path));}
  public string BlobPath(string hash){
   if(hash==null||hash.Length!=64)throw new ArgumentException("Invalid hash");
   foreach(char c in hash)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))throw new ArgumentException("Invalid hash");
   return Path.Combine(At("blobs"),hash);
  }
  static string Hash(Stream stream){return ResourceFiles.Hash(stream);}
  public bool HasVerified(string hash,long size){
   string path=BlobPath(hash);
   if(!File.Exists(path))return false;
   using(FileStream input=File.OpenRead(path))return input.Length==size&&Hash(input)==hash;
  }
  public void PutVerified(string hash,long size,byte[] bytes){
   string path=BlobPath(hash);
   using(MemoryStream input=new MemoryStream(bytes))if(bytes.LongLength!=size||Hash(input)!=hash)throw new IOException("Resource integrity failure");
   if(File.Exists(path)&&HasVerified(hash,size))return;
   string pending=path+".pending";WriteBytes(pending,bytes);
   if(File.Exists(path))File.Delete(path);ResourceFiles.Move(pending,path);
  }
  public string Recover(){
   string active=At("active"),previous=At("previous");
   if(File.Exists(At("journal"))){
    if(!File.Exists(Path.Combine(active,"accepted"))||ResourceFiles.ReadText(Path.Combine(active,"accepted"))!="accepted"){Remove(active);if(Directory.Exists(previous))ResourceFiles.Move(previous,active);}
    File.Delete(At("journal"));
   }
   Remove(At("staged"));ResourceFiles.SyncDirectory(root);
   string manifest=Path.Combine(active,"manifest");
   return File.Exists(manifest)?ResourceFiles.ReadText(manifest):null;
  }
  public void Stage(string snapshot,string[] hashes,string manifest){
   foreach(string hash in hashes){string path=BlobPath(hash);if(!File.Exists(path)||!HasVerified(hash,ResourceFiles.Length(path)))throw new IOException("Incomplete resource generation");}
   if(File.Exists(At("journal")))throw new InvalidOperationException("Activation already pending");
   Remove(At("staged"));Directory.CreateDirectory(At("staged"));
   Write(Path.Combine(At("staged"),"snapshot"),snapshot);Write(Path.Combine(At("staged"),"manifest"),manifest);
  }
  public void Commit(){
   if(!Directory.Exists(At("staged")))throw new InvalidOperationException("No staged generation");
   Write(At("journal"),"activating");Remove(At("previous"));
   if(Directory.Exists(At("active")))ResourceFiles.Move(At("active"),At("previous"));
   ResourceFiles.Move(At("staged"),At("active"));
  }
  public string[] ReadHistory(){
   string history=At("history");if(!Directory.Exists(history))return new string[0];
   string[] files=ResourceFiles.Manifests(history);Array.Sort(files);Array.Reverse(files);
   string[] result=new string[files.Length];for(int i=0;i<files.Length;i++)result[i]=ResourceFiles.ReadText(files[i]);return result;
  }
  public void Accept(){
   if(!File.Exists(At("journal")))throw new InvalidOperationException("No activation");
   string history=At("history");Directory.CreateDirectory(history);
   string[] files=ResourceFiles.Manifests(history);Array.Sort(files);
   long sequence=files.Length==0?1:Int64.Parse(Path.GetFileName(files[files.Length-1]).Substring(0,20))+1;
   string pending=Path.Combine(history,"pending");Write(pending,ResourceFiles.ReadText(Path.Combine(At("active"),"manifest")));
   Write(Path.Combine(At("active"),"accepted"),"accepted");
   Warning=null;try{ResourceFiles.Move(pending,Path.Combine(history,sequence.ToString("D20")+".manifest"));File.Delete(At("journal"));ResourceFiles.SyncDirectory(root);}catch(Exception error){Warning="Accepted generation cleanup: "+error.GetType().Name;}
  }
  public string Rollback(){if(File.Exists(At("journal"))){Remove(At("active"));if(Directory.Exists(At("previous")))ResourceFiles.Move(At("previous"),At("active"));File.Delete(At("journal"));}return Recover();}
 }
}
