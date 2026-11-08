using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace RTD.ResourceUpdates {
 // Files are immutable; generation directories are renamed on the same volume.
 // The journal remains until memory activation has been accepted.
 public sealed class ResourceStore {
  readonly string root;
  public ResourceStore(string root){this.root=root;Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(root,"blobs"));}
  string At(string name){return Path.Combine(root,name);}
  static void Remove(string path){if(Directory.Exists(path))Directory.Delete(path,true);}
  static void Write(string path,string value){WriteBytes(path,Encoding.UTF8.GetBytes(value));}
  static void WriteBytes(string path,byte[] value){using(FileStream f=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(value,0,value.Length);f.Flush();}}
  public string BlobPath(string hash){
   if(hash==null||hash.Length!=64)throw new ArgumentException("Invalid hash");
   foreach(char c in hash)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))throw new ArgumentException("Invalid hash");
   return Path.Combine(At("blobs"),hash);
  }
  static string Hash(Stream stream){using(SHA256 hash=SHA256.Create()){byte[] bytes=hash.ComputeHash(stream);return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}}
  public bool HasVerified(string hash,long size){
   string path=BlobPath(hash);
   if(!File.Exists(path))return false;
   using(FileStream input=File.OpenRead(path))return input.Length==size&&Hash(input)==hash;
  }
  public void PutVerified(string hash,long size,byte[] bytes){
   string path=BlobPath(hash);
   using(MemoryStream input=new MemoryStream(bytes))if(bytes.LongLength!=size||Hash(input)!=hash)throw new InvalidDataException("Resource integrity failure");
   if(File.Exists(path)&&HasVerified(hash,size))return;
   string pending=path+".pending";WriteBytes(pending,bytes);
   if(File.Exists(path))File.Delete(path);File.Move(pending,path);
  }
  public string Recover(){
   string active=At("active"),previous=At("previous");
   if(File.Exists(At("journal"))){
    if(!File.Exists(Path.Combine(active,"accepted"))){Remove(active);if(Directory.Exists(previous))Directory.Move(previous,active);}
    File.Delete(At("journal"));
   }
   Remove(At("staged"));
   string manifest=Path.Combine(active,"manifest");
   return File.Exists(manifest)?File.ReadAllText(manifest):null;
  }
  public void Stage(string snapshot,string[] hashes,string manifest){
   foreach(string hash in hashes){string path=BlobPath(hash);if(!File.Exists(path)||!HasVerified(hash,new FileInfo(path).Length))throw new InvalidDataException("Incomplete resource generation");}
   if(File.Exists(At("journal")))throw new InvalidOperationException("Activation already pending");
   Remove(At("staged"));Directory.CreateDirectory(At("staged"));
   Write(Path.Combine(At("staged"),"snapshot"),snapshot);Write(Path.Combine(At("staged"),"manifest"),manifest);
  }
  public void Commit(){
   if(!Directory.Exists(At("staged")))throw new InvalidOperationException("No staged generation");
   Write(At("journal"),"activating");Remove(At("previous"));
   if(Directory.Exists(At("active")))Directory.Move(At("active"),At("previous"));
   Directory.Move(At("staged"),At("active"));
  }
  public void Accept(){
   if(!File.Exists(At("journal")))throw new InvalidOperationException("No activation");
   Write(Path.Combine(At("active"),"accepted"),"accepted");File.Delete(At("journal"));
  }
  public string Rollback(){return Recover();}
 }
}
